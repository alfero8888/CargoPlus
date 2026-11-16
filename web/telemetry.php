<?php
$pagename = "telemetry";
if (session_status() === PHP_SESSION_NONE) {
    session_start();
}
require_once(__DIR__ . '/conn.php');

if (!isset($_SESSION['logged_user'])) {
    header('Location: index.php');
    exit;
}
if ((int)$_SESSION['logged_user']->priv === 4) {
    header('Location: vrazsofor.php');
    exit;
}

function telemetry_ids(array $raw): array
{
    $ids = [];
    foreach ($raw as $id) {
        $number = (int)$id;
        if ($number > 0) {
            $ids[$number] = $number;
        }
    }
    return array_values($ids);
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['delete_ids'])) {
    $ids = telemetry_ids((array)$_POST['delete_ids']);
    if ($ids !== []) {
        $stmt = mysqli_prepare($linkcon, "DELETE FROM cargo_telemetry_logs WHERE id = ?");
        foreach ($ids as $id) {
            mysqli_stmt_bind_param($stmt, 'i', $id);
            mysqli_stmt_execute($stmt);
        }
        mysqli_stmt_close($stmt);
    }
    $back = 'telemetry.php';
    if (!empty($_POST['branch'])) {
        $back .= '?branch=' . urlencode((string)$_POST['branch']);
    }
    header('Location: ' . $back);
    exit;
}

$branch = trim((string)($_GET['branch'] ?? ''));

function telemetry_rows(mysqli $link, string $branch): array
{
    if ($branch === '') {
        $sql = "SELECT id, branch_name, hostname, ip_address, app_version, cpu_usage, ram_usage, sys_cpu, free_ram_mb, free_disk_gb, top_ram_processes, errors_log, created_at
                FROM cargo_telemetry_logs ORDER BY id DESC LIMIT 200";
        $stmt = mysqli_prepare($link, $sql);
    } else {
        $sql = "SELECT id, branch_name, hostname, ip_address, app_version, cpu_usage, ram_usage, sys_cpu, free_ram_mb, free_disk_gb, top_ram_processes, errors_log, created_at
                FROM cargo_telemetry_logs WHERE branch_name = ? ORDER BY id DESC LIMIT 200";
        $stmt = mysqli_prepare($link, $sql);
        mysqli_stmt_bind_param($stmt, 's', $branch);
    }
    mysqli_stmt_execute($stmt);
    mysqli_stmt_bind_result($stmt, $id, $branchName, $hostname, $ip, $version, $cpu, $ram, $sysCpu, $freeRam, $disk, $topRam, $errorsLog, $createdAt);
    $rows = [];
    while (mysqli_stmt_fetch($stmt)) {
        $rows[] = [
            'id' => $id,
            'branch_name' => $branchName,
            'hostname' => $hostname,
            'ip_address' => $ip,
            'app_version' => $version,
            'cpu_usage' => $cpu,
            'ram_usage' => $ram,
            'sys_cpu' => $sysCpu,
            'free_ram_mb' => $freeRam,
            'free_disk_gb' => $disk,
            'top_ram_processes' => $topRam,
            'errors_log' => $errorsLog,
            'created_at' => $createdAt,
        ];
    }
    mysqli_stmt_close($stmt);
    return $rows;
}

if (isset($_GET['export'])) {
    $rows = telemetry_rows($linkcon, $branch);
    header('Content-Type: application/vnd.ms-excel; charset=utf-8');
    header('Content-Disposition: attachment; filename="telemetry.xls"');
    echo "\xEF\xBB\xBF";
    echo "<table border='1'><tr><th>ID</th><th>Sube</th><th>Bilgisayar</th><th>IP</th><th>Surum</th><th>Uygulama CPU</th><th>Uygulama RAM</th><th>PC CPU</th><th>Bos RAM</th><th>Disk</th><th>Zaman</th></tr>";
    foreach ($rows as $row) {
        echo '<tr>';
        foreach (['id', 'branch_name', 'hostname', 'ip_address', 'app_version', 'cpu_usage', 'ram_usage', 'sys_cpu', 'free_ram_mb', 'free_disk_gb', 'created_at'] as $key) {
            echo '<td>' . htmlspecialchars((string)$row[$key], ENT_QUOTES) . '</td>';
        }
        echo '</tr>';
    }
    echo '</table>';
    exit;
}

require_once(__DIR__ . '/header.php');

mysqli_query($linkcon, "CREATE TABLE IF NOT EXISTS cargo_telemetry_logs (
    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
    branch_name VARCHAR(120) NOT NULL DEFAULT '',
    hostname VARCHAR(120) NOT NULL DEFAULT '',
    ip_address VARCHAR(64) NOT NULL DEFAULT '',
    app_version VARCHAR(32) NOT NULL DEFAULT '',
    cpu_usage DECIMAL(5,1) NOT NULL DEFAULT 0,
    ram_usage DECIMAL(8,1) NOT NULL DEFAULT 0,
    sys_cpu DECIMAL(5,1) NOT NULL DEFAULT 0,
    free_ram_mb INT NOT NULL DEFAULT 0,
    free_disk_gb DECIMAL(8,1) NOT NULL DEFAULT 0,
    top_cpu_processes TEXT NULL,
    top_ram_processes TEXT NULL,
    errors_log TEXT NULL,
    created_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    KEY branch_time (branch_name, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

$rows = telemetry_rows($linkcon, $branch);
$branches = [];
$names = mysqli_query($linkcon, "SELECT DISTINCT branch_name FROM cargo_telemetry_logs ORDER BY branch_name");
if ($names) {
    while ($name = mysqli_fetch_row($names)) {
        $branches[] = $name[0];
    }
}

function telemetry_status(array $row): string
{
    $age = time() - strtotime((string)$row['created_at']);
    $errors = json_decode((string)$row['errors_log'], true);
    $hot = (float)$row['sys_cpu'] >= 85 || (float)$row['cpu_usage'] >= 70 || (int)$row['free_ram_mb'] < 400;
    if ($age > 1200) {
        return 'Offline';
    }
    if ($hot || (is_array($errors) && $errors !== [])) {
        return 'Warning';
    }
    return 'Online';
}

function telemetry_apps(array $row): array
{
    $list = json_decode((string)$row['top_ram_processes'], true);
    if (!is_array($list)) {
        return [];
    }
    $apps = [];
    foreach ($list as $item) {
        if (!is_array($item)) {
            continue;
        }
        $apps[] = [
            'name' => (string)($item['name'] ?? ''),
            'cpu' => (float)($item['cpu'] ?? 0),
            'mb' => (float)($item['mb'] ?? 0),
        ];
    }
    return $apps;
}

function telemetry_h($text)
{
    return htmlspecialchars((string)$text, ENT_QUOTES);
}

$groups = [];
foreach ($rows as $row) {
    $groups[$row['branch_name'] . '|' . $row['hostname']][] = $row;
}
$latest = [];
foreach ($groups as $key => $items) {
    $latest[$key] = $items[0];
}
$chartLabels = [];
$chartCpu = [];
$chartRam = [];
foreach ($latest as $row) {
    $chartLabels[] = $row['branch_name'] . ' / ' . $row['hostname'];
    $chartCpu[] = (float)$row['sys_cpu'];
    $chartRam[] = (float)$row['ram_usage'];
}
?>
<main class="tl">
   <div class="tl-bar">
      <div>
         <h1>Telemetri</h1>
         <p><?= count($groups) ?> bilgisayar · <?= count($rows) ?> gonderim</p>
      </div>
      <div class="tl-actions">
         <form method="get" action="telemetry.php">
            <select name="branch" onchange="this.form.submit()">
               <option value="">Tum subeler</option>
               <?php foreach ($branches as $name): ?>
                  <option value="<?= telemetry_h($name) ?>" <?= $name === $branch ? 'selected' : '' ?>><?= telemetry_h($name) ?></option>
               <?php endforeach; ?>
            </select>
         </form>
         <a class="tl-btn" href="telemetry.php?export=1<?= $branch === '' ? '' : '&branch=' . urlencode($branch) ?>">Excel</a>
         <button type="button" class="tl-btn tl-btn-danger" id="deleteSelected">Sil</button>
      </div>
   </div>
   <div class="tl-charts">
      <div class="tl-panel"><canvas id="cpuChart" height="120"></canvas></div>
      <div class="tl-panel"><canvas id="ramChart" height="120"></canvas></div>
   </div>
   <form method="post" id="telemetryForm" action="telemetry.php">
      <input type="hidden" name="branch" value="<?= telemetry_h($branch) ?>">
      <label class="tl-all"><input type="checkbox" id="pickAll"> Gorunen kayitlarin hepsini sec</label>
      <?php foreach ($groups as $items):
         $head = $items[0];
         $state = telemetry_status($head);
      ?>
         <details class="tl-card">
            <summary>
               <span class="tl-dot tl-<?= $state ?>"></span>
               <span class="tl-title">
                  <b><?= telemetry_h($head['branch_name']) ?></b>
                  <small><?= telemetry_h($head['hostname']) ?> · <?= telemetry_h($head['ip_address']) ?> · <?= telemetry_h($head['app_version']) ?></small>
               </span>
               <span class="tl-metrics">
                  <span>CPU <?= telemetry_h($head['sys_cpu']) ?>%</span>
                  <span>RAM <?= telemetry_h($head['ram_usage']) ?> MB</span>
                  <span>Disk <?= telemetry_h($head['free_disk_gb']) ?> GB</span>
               </span>
               <span class="tl-when"><?= telemetry_h($head['created_at']) ?> · <?= count($items) ?></span>
            </summary>
            <div class="tl-body">
               <?php foreach ($items as $row):
                  $apps = telemetry_apps($row);
                  $errors = json_decode((string)$row['errors_log'], true);
                  $maxMb = 1;
                  foreach ($apps as $app) {
                     if ($app['mb'] > $maxMb) {
                        $maxMb = $app['mb'];
                     }
                  }
               ?>
                  <details class="tl-send">
                     <summary>
                        <input type="checkbox" name="delete_ids[]" value="<?= (int)$row['id'] ?>" class="row-pick" onclick="event.stopPropagation()">
                        <span class="tl-dot tl-<?= telemetry_status($row) ?>"></span>
                        <b><?= telemetry_h($row['created_at']) ?></b>
                        <span>uygulama <?= telemetry_h($row['cpu_usage']) ?>% · <?= telemetry_h($row['ram_usage']) ?> MB</span>
                        <span>bos RAM <?= telemetry_h($row['free_ram_mb']) ?> MB</span>
                        <span><?= count($apps) ?> program</span>
                     </summary>
                     <?php if (!is_array($errors) || $errors === []): ?>
                     <?php else: ?>
                        <pre class="tl-error"><?= telemetry_h($row['errors_log']) ?></pre>
                     <?php endif; ?>
                     <?php if ($apps === []): ?>
                        <p class="tl-empty">Bu gonderimde program listesi yok.</p>
                     <?php else: ?>
                        <div class="tl-apps">
                           <?php foreach ($apps as $app): ?>
                              <div class="tl-app">
                                 <span><?= telemetry_h($app['name']) ?></span>
                                 <span class="tl-track"><i style="width:<?= min(100, $app['mb'] / $maxMb * 100) ?>%"></i></span>
                                 <em><?= telemetry_h($app['mb']) ?> MB · <?= telemetry_h($app['cpu']) ?>%</em>
                              </div>
                           <?php endforeach; ?>
                        </div>
                     <?php endif; ?>
                  </details>
               <?php endforeach; ?>
            </div>
         </details>
      <?php endforeach; ?>
   </form>
</main>
<style>
.tl { margin: 88px auto 40px; max-width: 1080px; padding: 0 20px; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; color: #1d1d1f; }
.tl-bar, .tl-actions { display: flex; gap: 10px; align-items: center; justify-content: space-between; flex-wrap: wrap; }
.tl h1 { font-size: 28px; font-weight: 650; letter-spacing: -0.4px; margin: 0; }
.tl-bar p, .tl-title small, .tl-when, .tl-metrics, .tl-empty { color: #6e6e73; }
.tl-bar p { margin: 2px 0 0; }
.tl-btn, .tl select { border: 0; background: #fff; color: #1d1d1f; border-radius: 999px; padding: 8px 14px; box-shadow: 0 1px 2px rgba(0,0,0,.06); text-decoration: none; }
.tl-btn-danger { color: #ff3b30; }
.tl-charts { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 18px 0; }
.tl-panel, .tl-card { background: #fff; border-radius: 18px; box-shadow: 0 8px 28px rgba(0,0,0,.05); }
.tl-panel { padding: 14px; }
.tl-card { margin-bottom: 12px; }
.tl-card > summary, .tl-send > summary { list-style: none; cursor: pointer; display: flex; gap: 12px; align-items: center; padding: 14px 16px; }
.tl-card > summary::-webkit-details-marker, .tl-send > summary::-webkit-details-marker { display: none; }
.tl-title { display: flex; flex-direction: column; min-width: 180px; }
.tl-title b { font-size: 16px; }
.tl-metrics { margin-left: auto; display: flex; gap: 14px; font-size: 13px; }
.tl-dot { width: 10px; height: 10px; border-radius: 50%; background: #8e8e93; flex: none; }
.tl-Online { background: #34c759; } .tl-Warning { background: #ff9f0a; } .tl-Offline { background: #8e8e93; }
.tl-body { border-top: 1px solid #f0f0f2; }
.tl-send { border-top: 1px solid #f6f6f8; }
.tl-send > summary { font-size: 13px; }
.tl-apps { display: grid; gap: 8px; padding: 0 16px 14px 42px; }
.tl-app { display: grid; grid-template-columns: 140px 1fr auto; gap: 10px; align-items: center; font-size: 13px; }
.tl-track { height: 6px; background: #f2f2f7; border-radius: 99px; overflow: hidden; }
.tl-track i { display: block; height: 6px; background: linear-gradient(90deg, #64d2ff, #0a84ff); }
.tl-error { margin: 0 16px 12px 42px; background: #fff4f4; color: #d70015; border-radius: 12px; padding: 10px; white-space: pre-wrap; }
.tl-all { display: flex; gap: 8px; align-items: center; color: #6e6e73; font-size: 13px; margin-bottom: 8px; }
.tl-empty { padding: 0 16px 12px 42px; }
@media (max-width: 800px) { .tl-charts, .tl-app { grid-template-columns: 1fr; } .tl-metrics { display: none; } }
</style>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script>
const labels = <?= json_encode($chartLabels, JSON_UNESCAPED_UNICODE) ?>;
const cpu = <?= json_encode($chartCpu) ?>;
const ram = <?= json_encode($chartRam) ?>;
if (labels.length) {
   new Chart(document.getElementById('cpuChart'), {
      type: 'bar',
      data: { labels, datasets: [{ label: 'PC CPU %', data: cpu, backgroundColor: '#0dcaf0' }] },
      options: { plugins: { legend: { labels: { color: '#6e6e73', boxWidth: 10 } } }, scales: { x: { ticks: { color: '#6e6e73' }, grid: { display: false } }, y: { beginAtZero: true, max: 100, ticks: { color: '#6e6e73' }, grid: { color: '#f2f2f7' } } } }
   });
   new Chart(document.getElementById('ramChart'), {
      type: 'bar',
      data: { labels, datasets: [{ label: 'Cargo RAM MB', data: ram, backgroundColor: '#0d6efd' }] },
      options: { plugins: { legend: { labels: { color: '#6e6e73', boxWidth: 10 } } }, scales: { x: { ticks: { color: '#6e6e73' }, grid: { display: false } }, y: { beginAtZero: true, ticks: { color: '#6e6e73' }, grid: { color: '#f2f2f7' } } } }
   });
}
document.getElementById('pickAll').addEventListener('change', (event) => {
   document.querySelectorAll('.row-pick').forEach((box) => { box.checked = event.target.checked; });
});
document.getElementById('deleteSelected').addEventListener('click', () => {
   const picked = document.querySelectorAll('.row-pick:checked').length;
   if (!picked) {
      Swal.fire({ icon: 'info', title: 'Satir secilmedi' });
      return;
   }
   Swal.fire({
      icon: 'warning',
      title: 'Secilen kayitlar silinsin mi?',
      text: picked + ' satir',
      showCancelButton: true,
      confirmButtonText: 'Sil',
      cancelButtonText: 'Vazgec'
   }).then((result) => {
      if (result.isConfirmed) {
         document.getElementById('telemetryForm').submit();
      }
   });
});
</script>
<?php mysqli_close($linkcon); ?>
</body>
<?php include 'footer.php'; ?>
</html>
