<?php
ini_set('memory_limit', '128M');
set_time_limit(30);
require 'vendor/autoload.php'; // Composer autoload

use PhpOffice\PhpSpreadsheet\IOFactory;

require_once __DIR__ . '/socialcalc.inc';
require_once __DIR__ . '/sheetnode_phpexcel.import.inc';

$inputFile = null;
$isTempFile = false;

// Support CLI usage (php import.php <filename>) and HTTP uploads
if (php_sapi_name() === 'cli') {
    $inputFile = isset($argv[1]) ? $argv[1] : null;
} else {
    // HTTP mode: accept uploaded file via multipart/form-data (field 'file' or 'upload')
    $fieldName = null;
    if (isset($_FILES['file']) && $_FILES['file']['error'] === UPLOAD_ERR_OK) {
        $fieldName = 'file';
    } elseif (isset($_FILES['upload']) && $_FILES['upload']['error'] === UPLOAD_ERR_OK) {
        $fieldName = 'upload';
    }

    if ($fieldName) {
        $tmpName = $_FILES[$fieldName]['tmp_name'];
        $origName = uniqid('', true) . '.tmp';
        $targetDir = __DIR__ . '/tmp';
        if (!is_dir($targetDir)) {
            mkdir($targetDir, 0755, true);
        }
        $targetPath = $targetDir . '/' . $origName;
        if (!move_uploaded_file($tmpName, $targetPath)) {
            http_response_code(500);
            header('Content-Type: application/json');
            echo json_encode(['error' => 'Failed to move uploaded file.']);
            exit;
        }
        $inputFile = $targetPath;
        $isTempFile = true;
    } else {
        http_response_code(400);
        header('Content-Type: application/json');
        echo json_encode(['error' => 'No input file provided. Use multipart form field "file" or "upload".']);
        exit;
    }
}

if (empty($inputFile) || !file_exists($inputFile)) {
    if (php_sapi_name() === 'cli') {
        fwrite(STDERR, "No input file specified or file does not exist.\n");
        exit(1);
    } else {
        http_response_code(400);
        header('Content-Type: application/json');
        echo json_encode(['error' => 'Input file not found.']);
        exit;
    }
}

try {
    $spreadsheet = IOFactory::load($inputFile);

    $sheetCount = $spreadsheet->getSheetCount();

    $book = [
        'numsheets' => $sheetCount,
        'currentname' => $spreadsheet->getActiveSheet()->getTitle(),
        'sheetArr' => [],
    ];

    foreach ($spreadsheet->getSheetNames() as $index => $sheetName) {
        $sheet = $spreadsheet->getSheet($index);
        $sheetSave = _sheetnode_phpexcel_import_do($spreadsheet, $sheet);

        $sheetData = [
            'name' => $sheetName,
            'sheetstr' => [
                'savestr' => $sheetSave,
            ],
        ];

        $book['sheetArr']["Sheet$index"] = $sheetData;

        if ($sheetName == $book['currentname']) {
            $book['currentid'] = "Sheet$index";
        }
    }

    $json = json_encode($book);

    if ($isTempFile && !empty($inputFile) && file_exists($inputFile)) {
        @unlink($inputFile);
    }

    if (php_sapi_name() === 'cli') {
        $outputFile = isset($argv[2]) ? $argv[2] : null;
        if (!empty($outputFile)) {
            file_put_contents($outputFile, $json);
        } else {
            // Preserve original CLI output format
            echo "$---$";
            echo $json;
        }
    } else {
        header('Content-Type: application/json');
        echo $json;
    }
} catch (Throwable $e) {
    if ($isTempFile && !empty($inputFile) && file_exists($inputFile)) {
        @unlink($inputFile);
    }
    if (php_sapi_name() === 'cli') {
        fwrite(STDERR, "Error processing file: " . $e->getMessage() . "\n");
        exit(1);
    } else {
        http_response_code(500);
        header('Content-Type: application/json');
        echo json_encode(['error' => 'Processing error', 'message' => $e->getMessage()]);
        exit;
    }
}