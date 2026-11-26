<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['ok' => false, 'error' => 'POST only']);
    exit;
}

$raw = file_get_contents('php://input');
if ($raw === false || strlen($raw) > 200000) {
    http_response_code(413);
    echo json_encode(['ok' => false, 'error' => 'payload']);
    exit;
}

$packets = json_decode($raw, true);
if (!is_array($packets)) {
    http_response_code(400);
    echo json_encode(['ok' => false, 'error' => 'json']);
    exit;
}

if ($packets !== [] && !array_is_list($packets)) {
    $packets = [$packets];
}

$config = require dirname(__DIR__) . '/config.php';
$mysqli = new mysqli($config['host'], $config['user'], $config['password'], $config['database'], (int)$config['port']);
if ($mysqli->connect_errno) {
    http_response_code(500);
    echo json_encode(['ok' => false]);
    exit;
}

$mysqli->set_charset('utf8mb4');
$schemaStamp = sys_get_temp_dir() . '/ubl_st_' . md5(__FILE__) . '_schema_v1';
if (!is_file($schemaStamp)) {   // таблицы — один раз на сервер (метка-файл), не на каждый запрос
$mysqli->query(<<<'SQL'
CREATE TABLE IF NOT EXISTS cargo_telemetry_logs (
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
SQL);
@touch($schemaStamp); }

$insert = $mysqli->prepare(<<<'SQL'
INSERT INTO cargo_telemetry_logs (
    branch_name, hostname, ip_address, app_version, cpu_usage, ram_usage,
    sys_cpu, free_ram_mb, free_disk_gb, top_cpu_processes, top_ram_processes, errors_log, created_at
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
SQL);

$saved = 0;
foreach (array_slice($packets, 0, 48) as $packet) {
    if (!is_array($packet)) {
        continue;
    }
    $branch = clip((string)($packet['branch'] ?? ''), 120);
    $hostname = clip((string)($packet['hostname'] ?? ''), 120);
    $ip = clip((string)($packet['ip'] ?? ''), 64);
    $version = clip((string)($packet['version'] ?? ''), 32);
    $cpu = num($packet['cpu'] ?? 0);
    $ram = num($packet['ram'] ?? 0);
    $sysCpu = num($packet['sysCpu'] ?? 0);
    $freeRam = (int)($packet['freeRam'] ?? 0);
    $disk = num($packet['disk'] ?? 0);
    $topCpu = json_encode($packet['topCpu'] ?? [], JSON_UNESCAPED_UNICODE);
    $topRam = json_encode($packet['topRam'] ?? [], JSON_UNESCAPED_UNICODE);
    $errors = json_encode($packet['errors'] ?? [], JSON_UNESCAPED_UNICODE);
    $created = date('Y-m-d H:i:s');
    $insert->bind_param(
        'ssssdddidssss',
        $branch,
        $hostname,
        $ip,
        $version,
        $cpu,
        $ram,
        $sysCpu,
        $freeRam,
        $disk,
        $topCpu,
        $topRam,
        $errors,
        $created
    );
    if ($insert->execute()) {
        $saved++;
    }
}

echo json_encode(['ok' => true, 'saved' => $saved]);

function clip(string $text, int $max): string
{
    $text = trim($text);
    return strlen($text) <= $max ? $text : substr($text, 0, $max);
}

function num(mixed $value): float
{
    return is_numeric($value) ? (float)$value : 0.0;
}
