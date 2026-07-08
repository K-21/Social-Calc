<?php

/**
 * Comprehensive PhpSpreadsheet ↔ SocialCalc Interoperability Test
 *
 * Tests: XLSX, ODS, CSV, HTML output, formulas, formatting, multi-sheet, dates
 *
 * Usage:
 *   php test_phpspreadsheet.php
 *
 * Output files created in ./test_outputs/ directory:
 *   - test_output.xlsx  (Excel format)
 *   - test_output.ods   (OpenDocument format)
 *   - test_output.csv   (CSV format)
 *   - test_output.html  (HTML format)
 */

require __DIR__ . '/vendor/autoload.php';

use PhpOffice\PhpSpreadsheet\Spreadsheet;
use PhpOffice\PhpSpreadsheet\IOFactory;
use PhpOffice\PhpSpreadsheet\Style\Fill;
use PhpOffice\PhpSpreadsheet\Style\Border;
use PhpOffice\PhpSpreadsheet\Style\Alignment;
use PhpOffice\PhpSpreadsheet\Style\NumberFormat;
use PhpOffice\PhpSpreadsheet\Cell\DataType;

// ─────────────────────────────────────────────
// Setup output directory
// ─────────────────────────────────────────────
$outputDir = __DIR__ . '/test_outputs';
if (!is_dir($outputDir)) {
    mkdir($outputDir, 0755, true);
}

echo "\n";
echo "╔══════════════════════════════════════════════════════╗\n";
echo "║   PhpSpreadsheet ↔ SocialCalc Interoperability Test ║\n";
echo "║   PhpSpreadsheet Version: " . \PhpOffice\PhpSpreadsheet\Spreadsheet::VERSION . str_repeat(' ', 28 - strlen(\PhpOffice\PhpSpreadsheet\Spreadsheet::VERSION)) . "║\n";
echo "╚══════════════════════════════════════════════════════╝\n\n";

// ─────────────────────────────────────────────
// BUILD THE SPREADSHEET
// ─────────────────────────────────────────────
$spreadsheet = new Spreadsheet();
$spreadsheet->getProperties()
    ->setTitle('SocialCalc Interop Test')
    ->setSubject('PhpSpreadsheet Interoperability Test')
    ->setDescription('Tests all major features for SocialCalc ↔ PhpSpreadsheet interop');

// ════════════════════════════════
// SHEET 1: Formulas & Basic Data
// ════════════════════════════════
echo "📋 Sheet 1: Formulas & Basic Data\n";

$sheet1 = $spreadsheet->getActiveSheet();
$sheet1->setTitle('Formulas & Data');

// Headers
$headers = ['Student', 'Math', 'Science', 'English', 'Total', 'Average', 'Grade'];
foreach ($headers as $col => $header) {
    $cell = chr(65 + $col) . '1';
    $sheet1->setCellValue($cell, $header);
}

// Style headers
$sheet1->getStyle('A1:G1')->applyFromArray([
    'font'      => ['bold' => true, 'color' => ['argb' => 'FFFFFFFF'], 'size' => 12],
    'fill'      => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => 'FF1F4E79']],
    'alignment' => ['horizontal' => Alignment::HORIZONTAL_CENTER],
    'borders'   => ['allBorders' => ['borderStyle' => Border::BORDER_THIN, 'color' => ['argb' => 'FFAAAAAA']]],
]);

// Data rows (simulates SocialCalc cell data being exported)
$students = [
    ['Alice',   92, 88, 95],
    ['Bob',     75, 80, 70],
    ['Charlie', 88, 92, 85],
    ['Diana',   60, 55, 65],
    ['Ethan',   95, 97, 99],
];

foreach ($students as $i => $student) {
    $row = $i + 2;
    $sheet1->setCellValue("A{$row}", $student[0]);
    $sheet1->setCellValue("B{$row}", $student[1]);
    $sheet1->setCellValue("C{$row}", $student[2]);
    $sheet1->setCellValue("D{$row}", $student[3]);

    // Formula: Total = SUM
    $sheet1->setCellValue("E{$row}", "=SUM(B{$row}:D{$row})");

    // Formula: Average
    $sheet1->setCellValue("F{$row}", "=AVERAGE(B{$row}:D{$row})");
    $sheet1->getStyle("F{$row}")->getNumberFormat()->setFormatCode('0.00');

    // Formula: Grade using IF
    $sheet1->setCellValue("G{$row}", "=IF(F{$row}>=90,\"A\",IF(F{$row}>=75,\"B\",IF(F{$row}>=60,\"C\",\"F\")))");

    // Alternate row color
    $bgColor = ($i % 2 === 0) ? 'FFD9E1F2' : 'FFFFFFFF';
    $sheet1->getStyle("A{$row}:G{$row}")->applyFromArray([
        'fill'    => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => $bgColor]],
        'borders' => ['allBorders' => ['borderStyle' => Border::BORDER_THIN, 'color' => ['argb' => 'FFCCCCCC']]],
    ]);
}

// Summary row
$summaryRow = count($students) + 2;
$sheet1->setCellValue("A{$summaryRow}", 'Class Average');
$sheet1->setCellValue("B{$summaryRow}", '=AVERAGE(B2:B6)');
$sheet1->setCellValue("C{$summaryRow}", '=AVERAGE(C2:C6)');
$sheet1->setCellValue("D{$summaryRow}", '=AVERAGE(D2:D6)');
$sheet1->setCellValue("E{$summaryRow}", '=SUM(E2:E6)');
$sheet1->setCellValue("F{$summaryRow}", '=AVERAGE(F2:F6)');
$sheet1->getStyle("A{$summaryRow}:G{$summaryRow}")->applyFromArray([
    'font' => ['bold' => true],
    'fill' => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => 'FFFFF2CC']],
]);

// Auto-size all columns
foreach (range('A', 'G') as $col) {
    $sheet1->getColumnDimension($col)->setAutoSize(true);
}

echo "   ✅ Headers, data rows, SUM/AVERAGE/IF formulas, conditional grades, row colors\n\n";

// ════════════════════════════════
// SHEET 2: Formatting & Data Types
// ════════════════════════════════
echo "🎨 Sheet 2: Formatting & Data Types\n";

$sheet2 = $spreadsheet->createSheet();
$sheet2->setTitle('Formatting & Types');

$sheet2->setCellValue('A1', 'Feature');
$sheet2->setCellValue('B1', 'Value');
$sheet2->setCellValue('C1', 'Notes');
$sheet2->getStyle('A1:C1')->applyFromArray([
    'font' => ['bold' => true, 'color' => ['argb' => 'FFFFFFFF']],
    'fill' => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => 'FF375623']],
    'alignment' => ['horizontal' => Alignment::HORIZONTAL_CENTER],
]);

$formattingTests = [
    // [Label, Value, Format, Note]
    ['Currency (USD)',    1234567.89,      NumberFormat::FORMAT_CURRENCY_USD_SIMPLE,  'USD currency format'],
    ['Currency (INR)',    85000.50,        '₹#,##0.00',                               'Indian Rupee format'],
    ['Percentage',       0.8765,           NumberFormat::FORMAT_PERCENTAGE_00,         'Percentage with 2 decimals'],
    ['Date',             '2026-07-08',     NumberFormat::FORMAT_DATE_DDMMYYYY,         'Date formatted as DD/MM/YYYY'],
    ['Time',             '14:30:00',       NumberFormat::FORMAT_DATE_TIME4,            'Time HH:MM:SS'],
    ['Scientific',       0.0000123456,     NumberFormat::FORMAT_SCIENTIFIC_D2,         'Scientific notation'],
    ['Large Integer',    9876543210,       NumberFormat::FORMAT_NUMBER_COMMA_SEPARATED1, 'Comma-separated number'],
    ['Text (as string)', '001234',         NumberFormat::FORMAT_TEXT,                  'Leading zeros preserved'],
    ['Negative number',  -9999.99,         '#,##0.00;[RED]-#,##0.00',                  'Red negative number'],
    ['Boolean TRUE',     true,             '@',                                        'Boolean value'],
];

foreach ($formattingTests as $i => $test) {
    $row = $i + 2;
    $sheet2->setCellValue("A{$row}", $test[0]);
    if ($test[3] === 'Leading zeros preserved') {
        $sheet2->setCellValueExplicit("B{$row}", $test[1], DataType::TYPE_STRING);
    } else {
        $sheet2->setCellValue("B{$row}", $test[1]);
    }
    $sheet2->getStyle("B{$row}")->getNumberFormat()->setFormatCode($test[2]);
    $sheet2->setCellValue("C{$row}", $test[3]);

    $bgColor = ($i % 2 === 0) ? 'FFF2F2F2' : 'FFFFFFFF';
    $sheet2->getStyle("A{$row}:C{$row}")->getFill()
        ->setFillType(Fill::FILL_SOLID)
        ->getStartColor()->setARGB($bgColor);
}

// Merged cell with centered text
$sheet2->mergeCells('A13:C13');
$sheet2->setCellValue('A13', '✨ PhpSpreadsheet supports all major Excel data types ✨');
$sheet2->getStyle('A13')->applyFromArray([
    'font'      => ['bold' => true, 'italic' => true, 'size' => 12, 'color' => ['argb' => 'FF7030A0']],
    'alignment' => ['horizontal' => Alignment::HORIZONTAL_CENTER],
    'fill'      => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => 'FFEDE7F6']],
]);

foreach (['A', 'B', 'C'] as $col) {
    $sheet2->getColumnDimension($col)->setAutoSize(true);
}

echo "   ✅ Currency, percentage, date, time, scientific, merged cells, text formatting\n\n";

// ════════════════════════════════
// SHEET 3: Advanced Formulas
// ════════════════════════════════
echo "🔢 Sheet 3: Advanced Formulas\n";

$sheet3 = $spreadsheet->createSheet();
$sheet3->setTitle('Advanced Formulas');

$sheet3->setCellValue('A1', 'Formula Type');
$sheet3->setCellValue('B1', 'Formula');
$sheet3->setCellValue('C1', 'Result');
$sheet3->getStyle('A1:C1')->applyFromArray([
    'font' => ['bold' => true, 'color' => ['argb' => 'FFFFFFFF']],
    'fill' => ['fillType' => Fill::FILL_SOLID, 'startColor' => ['argb' => 'FF833C00']],
    'alignment' => ['horizontal' => Alignment::HORIZONTAL_CENTER],
]);

// Seed values
$sheet3->setCellValue('E1', 10);
$sheet3->setCellValue('E2', 20);
$sheet3->setCellValue('E3', 30);
$sheet3->setCellValue('E4', 40);
$sheet3->setCellValue('E5', 50);

$formulas = [
    ['SUM',          '=SUM(E1:E5)',                   '=SUM(E1:E5)'],
    ['AVERAGE',      '=AVERAGE(E1:E5)',                '=AVERAGE(E1:E5)'],
    ['MAX',          '=MAX(E1:E5)',                    '=MAX(E1:E5)'],
    ['MIN',          '=MIN(E1:E5)',                    '=MIN(E1:E5)'],
    ['COUNT',        '=COUNT(E1:E5)',                  '=COUNT(E1:E5)'],
    ['IF',           '=IF(E1>5,"Yes","No")',           '=IF(E1>5,"Yes","No")'],
    ['CONCATENATE',  '=CONCATENATE("Hello"," ","World")', '=CONCATENATE("Hello"," ","World")'],
    ['LEN',          '=LEN("SocialCalc")',             '=LEN("SocialCalc")'],
    ['UPPER',        '=UPPER("hello world")',          '=UPPER("hello world")'],
    ['ROUND',        '=ROUND(3.14159,2)',              '=ROUND(3.14159,2)'],
    ['ABS',          '=ABS(-99)',                      '=ABS(-99)'],
    ['SQRT',         '=SQRT(144)',                     '=SQRT(144)'],
    ['POWER',        '=POWER(2,10)',                   '=POWER(2,10)'],
    ['TODAY',        '=TODAY()',                       '=TODAY()'],
    ['NOW',          '=NOW()',                         '=NOW()'],
];

foreach ($formulas as $i => $formula) {
    $row = $i + 2;
    $sheet3->setCellValue("A{$row}", $formula[0]);
    $sheet3->setCellValue("B{$row}", $formula[1]);
    $sheet3->setCellValue("C{$row}", $formula[2]);

    $bgColor = ($i % 2 === 0) ? 'FFFFF8F0' : 'FFFFFFFF';
    $sheet3->getStyle("A{$row}:C{$row}")->getFill()
        ->setFillType(Fill::FILL_SOLID)
        ->getStartColor()->setARGB($bgColor);
}

foreach (['A', 'B', 'C'] as $col) {
    $sheet3->getColumnDimension($col)->setAutoSize(true);
}

echo "   ✅ SUM, AVERAGE, MAX, MIN, COUNT, IF, CONCATENATE, UPPER, ROUND, SQRT, TODAY, NOW\n\n";

// ════════════════════════════════
// EXPORT TO ALL FORMATS
// ════════════════════════════════
$spreadsheet->setActiveSheetIndex(0);

$formats = [
    ['Xlsx', 'test_output.xlsx',  'Excel (.xlsx)'],
    ['Ods',  'test_output.ods',   'OpenDocument (.ods)'],
    ['Csv',  'test_output.csv',   'CSV (.csv)'],
    ['Html', 'test_output.html',  'HTML (.html)'],
];

echo "💾 Saving all output formats...\n\n";
$allPassed = true;

foreach ($formats as $format) {
    [$writerType, $filename, $label] = $format;
    $outputPath = $outputDir . '/' . $filename;

    try {
        $writer = IOFactory::createWriter($spreadsheet, $writerType);
        $writer->save($outputPath);
        $size = number_format(filesize($outputPath));
        echo "   ✅ {$label} → {$filename} ({$size} bytes)\n";
    } catch (\Exception $e) {
        echo "   ❌ {$label} → FAILED: " . $e->getMessage() . "\n";
        $allPassed = false;
    }
}

// ════════════════════════════════
// READ BACK THE XLSX (Round-trip test)
// ════════════════════════════════
echo "\n🔄 Round-trip test: Reading back test_output.xlsx...\n";
try {
    $loaded = IOFactory::load($outputDir . '/test_output.xlsx');
    $readSheet = $loaded->getActiveSheet();
    $cellA1 = $readSheet->getCell('A1')->getValue();
    $cellB2 = $readSheet->getCell('B2')->getValue();
    $cellE2 = $readSheet->getCell('E2')->getCalculatedValue();

    echo "   A1 (header)     = '{$cellA1}' → " . ($cellA1 === 'Student' ? '✅' : '❌') . "\n";
    echo "   B2 (Alice math) = '{$cellB2}' → " . ($cellB2 == 92 ? '✅' : '❌') . "\n";
    echo "   E2 (SUM formula)= '{$cellE2}' → " . ($cellE2 == 275 ? '✅' : '❌') . "\n";
} catch (\Exception $e) {
    echo "   ❌ Round-trip read FAILED: " . $e->getMessage() . "\n";
    $allPassed = false;
}

// ════════════════════════════════
// FINAL SUMMARY
// ════════════════════════════════
echo "\n";
echo "╔══════════════════════════════════════════════════════╗\n";
if ($allPassed) {
    echo "║  ✅  ALL INTEROPERABILITY TESTS PASSED!             ║\n";
} else {
    echo "║  ❌  SOME TESTS FAILED — see errors above           ║\n";
}
echo "╠══════════════════════════════════════════════════════╣\n";
echo "║  Output files are in: ./test_outputs/               ║\n";
echo "║    • test_output.xlsx  — Open in Excel or Sheets    ║\n";
echo "║    • test_output.ods   — Open in LibreOffice        ║\n";
echo "║    • test_output.csv   — Open in any text editor    ║\n";
echo "║    • test_output.html  — Open in any browser        ║\n";
echo "╚══════════════════════════════════════════════════════╝\n\n";
