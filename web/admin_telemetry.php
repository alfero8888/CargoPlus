<?php
declare(strict_types=1);
session_start();
$config = require __DIR__ . '/config.php';

if (isset($_POST['gate'])) {
    $_SESSION['telemetry'] = hash_equals((string)$config['gate'], (string)$_POST['gate']);
}
if (empty($_SESSION['telemetry'])) {
    echo '<!doctype html><meta charset="utf-8"><title>Телеметрия</title><form method="post" style="font-family:Segoe UI;max-width:320px;margin:48px auto"><h1>Телеметрия</h1><input type="password" name="gate" autofocus style="width:100%;padding:8px"><button style="margin-top:12px">Войти</button></form>';
    exit;
}

$mysqli = new mysqli($config['host'], $config['user'], $config['password'], $config['database'], (int)$config['port']);
$mysqli->set_charset('utf8mb4');
$mysqli->query('CREATE TABLE IF NOT EXISTS cargo_telemetry_logs (
    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
    branch_name VARCHAR(120) NOT NULL DEFAULT \'\',
    hostname VARCHAR(120) NOT NULL DEFAULT \'\',
    ip_address VARCHAR(64) NOT NULL DEFAULT \'\',
    app_version VARCHAR(32) NOT NULL DEFAULT \'\',
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
$branch = trim((string)($_GET['branch'] ?? ''));
$where = $branch === '' ? '' : 'WHERE branch_name = ?';
$sql = "SELECT branch_name, hostname, ip_address, app_version, cpu_usage, ram_usage, sys_cpu, free_ram_mb, free_disk_gb, top_cpu_processes, top_ram_processes, errors_log, created_at FROM cargo_telemetry_logs $where ORDER BY id DESC LIMIT 200";
$stmt = $mysqli->prepare($sql);
if ($branch !== '') {
    $stmt->bind_param('s', $branch);
}
$stmt->execute();
$stmt->bind_result($branchName, $hostname, $ip, $version, $cpu, $ram, $sysCpu, $freeRam, $disk, $topCpu, $topRam, $errorsLog, $createdAt);
$rows = [];
while ($stmt->fetch()) {
    $rows[] = [
        'branch_name' => $branchName,
        'hostname' => $hostname,
        'ip_address' => $ip,
        'app_version' => $version,
        'cpu_usage' => $cpu,
        'ram_usage' => $ram,
        'sys_cpu' => $sysCpu,
        'free_ram_mb' => $freeRam,
        'free_disk_gb' => $disk,
        'top_cpu_processes' => $topCpu,
        'top_ram_processes' => $topRam,
        'errors_log' => $errorsLog,
        'created_at' => $createdAt,
    ];
}
$stmt->close();

$branches = [];
$names = $mysqli->query('SELECT DISTINCT branch_name FROM cargo_telemetry_logs ORDER BY branch_name');
if ($names) {
    while ($name = $names->fetch_row()) {
        $branches[] = $name[0];
    }
}

function status(array $row): string
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

function h(string $text): string
{
    return htmlspecialchars($text, ENT_QUOTES);
}

function apps(array $row): array
{
    $list = json_decode((string)$row['top_ram_processes'], true);
    if (!is_array($list)) {
        return [];
    }
    $rows = [];
    foreach ($list as $item) {
        if (!is_array($item)) {
            continue;
        }
        $rows[] = [
            'name' => (string)($item['name'] ?? ''),
            'cpu' => (float)($item['cpu'] ?? 0),
            'mb' => (float)($item['mb'] ?? 0),
        ];
    }
    return $rows;
}

function bar(float $value, float $max, string $color): string
{
    $width = $max <= 0 ? 0 : min(100, $value / $max * 100);
    return '<span class="bar"><span style="width:' . $width . '%;background:' . $color . '"></span></span>';
}

header('Content-Type: text/html; charset=utf-8');
?>
<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Телеметрия Cargo</title>
<style>
body { margin: 0; font-family: Segoe UI, sans-serif; background: #0f1724; color: #e8eef7; }
header { display: flex; justify-content: space-between; align-items: center; padding: 20px 28px; background: linear-gradient(90deg, #12355b, #1b6ca8); }
h1, h2 { margin: 0 0 12px; }
main { padding: 22px 28px 40px; }
select, button { background: #102033; color: white; border: 1px solid #3d5d80; border-radius: 8px; padding: 8px 10px; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px; margin: 16px 0 22px; }
.card { background: #17283d; border-radius: 14px; padding: 14px 16px; }
.card b { display: block; font-size: 22px; margin-top: 6px; }
.muted { color: #9fb0c5; font-size: 13px; }
table { width: 100%; border-collapse: collapse; background: #17283d; border-radius: 12px; overflow: hidden; margin-bottom: 22px; }
th { text-align: left; background: #21415f; }
th, td { padding: 9px 10px; border-bottom: 1px solid #24384f; font-size: 14px; }
.pill { border-radius: 999px; padding: 3px 10px; font-weight: 700; }
.Online { background: #14532d; color: #86efac; }
.Offline { background: #334155; color: #cbd5e1; }
.Warning { background: #7c2d12; color: #fdba74; }
.bar { display: inline-block; width: 120px; height: 8px; background: #0d1826; border-radius: 99px; vertical-align: middle; margin-right: 8px; }
.bar span { display: block; height: 8px; border-radius: 99px; }
.block { background: #121e30; border-radius: 16px; padding: 16px; margin-bottom: 18px; }
.error { background: #3f1d24; border-left: 4px solid #fb7185; padding: 10px 12px; margin: 8px 0; border-radius: 8px; }
</style>
</head>
<body>
<header>
<div><h1>Телеметрия отделений</h1><div class="muted">Компьютеры и телефоны в одной таблице</div></div>
<form method="get">
<select name="branch" onchange="this.form.submit()">
<option value="">Все отделения</option>
<?php foreach ($branches as $name): ?>
<option value="<?= h($name) ?>" <?= $name === $branch ? 'selected' : '' ?>><?= h($name) ?></option>
<?php endforeach; ?>
</select>
</form>
</header>
<main>
<?php
$latest = [];
foreach ($rows as $row) {
    $key = $row['branch_name'] . '|' . $row['hostname'];
    if (!isset($latest[$key])) {
        $latest[$key] = $row;
    }
}
?>
<div class="cards">
<?php foreach ($latest as $row): $state = status($row); ?>
<div class="card">
<span class="pill <?= $state ?>"><?= $state ?></span>
<b><?= h($row['branch_name']) ?></b>
<div class="muted"><?= h($row['hostname']) ?> · <?= h($row['ip_address']) ?> · <?= h($row['app_version']) ?></div>
<div style="margin-top:10px">CPU ПК <?= bar((float)$row['sys_cpu'], 100, '#38bdf8') ?> <?= h((string)$row['sys_cpu']) ?>%</div>
<div>Cargo <?= bar((float)$row['ram_usage'], 800, '#a78bfa') ?> <?= h((string)$row['ram_usage']) ?> МБ</div>
<div class="muted">Свободно <?= h((string)$row['free_ram_mb']) ?> МБ · диск <?= h((string)$row['free_disk_gb']) ?> ГБ · <?= h($row['created_at']) ?></div>
</div>
<?php endforeach; ?>
</div>
<div class="block">
<h2>Точки</h2>
<table>
<tr><th>Статус</th><th>Отделение</th><th>Компьютер</th><th>IP</th><th>Версия</th><th>CPU приложения</th><th>RAM приложения</th><th>CPU ПК</th><th>Свободно RAM</th><th>Диск</th><th>Время</th></tr>
<?php foreach ($rows as $row): $state = status($row); ?>
<tr>
<td><span class="pill <?= $state ?>"><?= $state ?></span></td>
<td><?= h($row['branch_name']) ?></td>
<td><?= h($row['hostname']) ?></td>
<td><?= h($row['ip_address']) ?></td>
<td><?= h($row['app_version']) ?></td>
<td><?= bar((float)$row['cpu_usage'], 100, '#22c55e') ?> <?= h((string)$row['cpu_usage']) ?>%</td>
<td><?= h((string)$row['ram_usage']) ?> МБ</td>
<td><?= bar((float)$row['sys_cpu'], 100, '#38bdf8') ?> <?= h((string)$row['sys_cpu']) ?>%</td>
<td><?= h((string)$row['free_ram_mb']) ?> МБ</td>
<td><?= h((string)$row['free_disk_gb']) ?> ГБ</td>
<td><?= h($row['created_at']) ?></td>
</tr>
<?php endforeach; ?>
</table>
</div>
<div class="block">
<h2>Что запущено на компьютере</h2>
<p class="muted">Список появится полностью после обновления программы до версии, которая присылает процессы. Старые записи показывают только самые тяжёлые.</p>
<?php foreach (array_slice($rows, 0, 12) as $row): $list = apps($row); if ($list === []) continue; $maxMb = max(array_column($list, 'mb')); ?>
<h3><?= h($row['branch_name'] . ' · ' . $row['hostname'] . ' · ' . $row['created_at']) ?></h3>
<table>
<tr><th>Программа</th><th>Память</th><th>Процессор</th></tr>
<?php foreach ($list as $app): ?>
<tr>
<td><?= h($app['name']) ?></td>
<td><?= bar($app['mb'], (float)$maxMb, '#f59e0b') ?> <?= h((string)$app['mb']) ?> МБ</td>
<td><?= bar($app['cpu'], 100, '#fb7185') ?> <?= h((string)$app['cpu']) ?>%</td>
</tr>
<?php endforeach; ?>
</table>
<?php endforeach; ?>
</div>
<div class="block">
<h2>Ошибки и баги</h2>
<?php $any = false; foreach ($rows as $row):
    $errors = json_decode((string)$row['errors_log'], true);
    if (!is_array($errors) || $errors === []) { continue; }
    $any = true;
?>
<div class="error"><b><?= h($row['branch_name'] . ' · ' . $row['created_at']) ?></b><pre><?= h((string)$row['errors_log']) ?></pre></div>
<?php endforeach; if (!$any): ?>
<p class="muted">Ошибок в последних записях нет.</p>
<?php endif; ?>
</div>
</main>
</body>
</html>
