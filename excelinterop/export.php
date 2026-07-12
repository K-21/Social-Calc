<?php
ini_set('memory_limit', '128M');
set_time_limit(30);
require 'vendor/autoload.php'; // Composer autoload

use PhpOffice\PhpSpreadsheet\Spreadsheet;
use PhpOffice\PhpSpreadsheet\IOFactory;

require_once __DIR__ . '/socialcalc.inc';
require_once __DIR__ . '/sheetnode_phpexcel.export.inc';

// Read the tmp file
$fname = $argv[1] ?? '';
$outfile = $argv[2] ?? '';
$outfiletype = $argv[3] ?? 'Xlsx';

$tmpDir = realpath(__DIR__ . '/tmp');
if (!$tmpDir) {
    error_log("tmp directory not found");
    exit(1);
}

// Validate that input and output are within tmp directory
if (empty($fname) || strpos(realpath(dirname($fname)), $tmpDir) !== 0) {
    error_log("Invalid input file path");
    exit(1);
}
if (empty($outfile) || strpos(realpath(dirname($outfile)), $tmpDir) !== 0) {
    error_log("Invalid output file path");
    exit(1);
}

try {

$fh = fopen($fname, "r");
$data = fread($fh, filesize($fname));
fclose($fh);



$book = json_decode($data);

// Debug: check if JSON decode succeeded
if ($book === null && json_last_error() !== JSON_ERROR_NONE) {
    error_log("JSON decode error: " . json_last_error_msg());
    error_log("Full data: " . $data);
    exit(1);
}

if (!isset($book->sheetArr)) {
    error_log("ERROR: sheetArr not found in data");
    error_log("Data structure keys: " . print_r(array_keys((array)$book), true));
    exit(1);
}

$sheetarr = $book->sheetArr;
$workbook = new Spreadsheet();

$sindex = 0;
$actualactiveindex = 0;

foreach ($sheetarr as $key => $value) {
    if ($sindex > 0) {
        $workbook->createSheet();
        $workbook->setActiveSheetIndex($sindex);
    }
    if ($key == $book->currentid) {
        $actualactiveindex = $sindex;
    }

    $title = $value->name;
    $sheet = socialcalc_parse_sheet($value->sheetstr->savestr);

    _sheetnode_phpexcel_export_do($workbook, $title, $sheet);

    $sindex++;
    echo $sindex . ' done' . PHP_EOL;
}

$workbook->setActiveSheetIndex($actualactiveindex);

// Write the workbook into a file
$objWriter = IOFactory::createWriter($workbook, $outfiletype);
$objWriter->save($outfile);

} catch (Throwable $e) {
    error_log("Export error: " . $e->getMessage());
    exit(1);
}
?>