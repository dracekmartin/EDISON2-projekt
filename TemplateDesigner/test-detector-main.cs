using UglyToad.PdfPig;
using TemplateDesigner.PdfImport;

// Spustí ScheduleDetectorTest na testovacích PDF
var pdf1 = @"c:\Users\miesb\Downloads\kv_zastavkovy.PDF";
var pdf2 = @"c:\Users\miesb\Downloads\kv_zastavkovy_2.PDF";

var sb = new System.Text.StringBuilder();

if (File.Exists(pdf1))
    sb.AppendLine(ScheduleDetectorTest.Run(pdf1));
if (File.Exists(pdf2))
    sb.AppendLine(ScheduleDetectorTest.Run(pdf2));

var output = sb.ToString();
Console.Write(output);

var outPath = Path.Combine(Path.GetDirectoryName(pdf1)!, "schedule-detect-results.txt");
File.WriteAllText(outPath, output);
Console.WriteLine($"\nVýsledky uloženy: {outPath}");
