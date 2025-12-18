/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    CELL TOWER TRAINING TOOL                                ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  CLI tool to download OpenCellID data and train RF fingerprint models.    ║
 * ║  Usage: dotnet run train-cells [options]                                  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Cellular;

public static class CellTrainingTool
{
    /// <summary>
    /// Runs the cell tower training workflow.
    /// </summary>
    public static async Task RunAsync(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("        NFRAME CELL TOWER TRAINING TOOL");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        // Parse arguments
        var mcc = GetArgValue(args, "--mcc", 310);           // Default: USA
        var maxRecords = GetArgValue(args, "--max", 50000);  // Training points
        var useSample = args.Contains("--sample");           // Use generated sample data
        var tokenArg = GetArgValueString(args, "--token");   // OpenCellID token
        
        var importer = new OpenCellIDImporter(tokenArg);
        var model = new RFLocatorModel();
        
        string csvPath;
        
        if (useSample || string.IsNullOrEmpty(tokenArg))
        {
            Console.WriteLine("◈ Using generated sample data (no API token)");
            Console.WriteLine("  For real data, get free token at: https://opencellid.org/register");
            Console.WriteLine("  Then run with: --token YOUR_TOKEN");
            Console.WriteLine();
            
            csvPath = await importer.GenerateSampleDataAsync(mcc);
        }
        else
        {
            Console.WriteLine($"◈ Downloading cell towers for MCC {mcc}...");
            csvPath = await importer.DownloadByCountryAsync(mcc);
        }
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("        IMPORTING DATA");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        var imported = await importer.ImportToModelAsync(model, csvPath, maxRecords);
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("        TRAINING MODEL");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        model.Train();
        
        // Save model
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models");
        Directory.CreateDirectory(modelDir);
        
        var modelPath = Path.Combine(modelDir, "rf_locator.json");
        model.SaveModel(modelPath);
        
        // Save training data for reference
        var dataPath = Path.Combine(modelDir, "training_data.csv");
        await model.SaveTrainingDataAsync(dataPath);
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("        TRAINING COMPLETE!");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"  Model saved to: {modelPath}");
        Console.WriteLine($"  Training data: {dataPath}");
        Console.WriteLine($"  Total points: {imported:N0}");
        Console.WriteLine();
        Console.WriteLine("  The model will be automatically loaded on next drone start.");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Interactive menu for training options.
    /// </summary>
    public static async Task RunInteractiveAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("        NFRAME CELL TOWER TRAINING");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  1. Train with sample data (USA - 10 major cities)");
        Console.WriteLine("  2. Train with sample data (UK)");
        Console.WriteLine("  3. Download OpenCellID data (requires free API token)");
        Console.WriteLine("  4. Import custom CSV file");
        Console.WriteLine("  5. Test existing model");
        Console.WriteLine("  0. Exit");
        Console.WriteLine();
        Console.Write("Select option: ");
        
        var choice = Console.ReadLine();
        
        switch (choice)
        {
            case "1":
                await RunAsync(new[] { "--sample", "--mcc", "310" });
                break;
                
            case "2":
                await RunAsync(new[] { "--sample", "--mcc", "234" });
                break;
                
            case "3":
                Console.Write("Enter OpenCellID token: ");
                var token = Console.ReadLine();
                Console.Write("Enter MCC (310=USA, 234=UK): ");
                var mcc = Console.ReadLine() ?? "310";
                await RunAsync(new[] { "--token", token ?? "", "--mcc", mcc });
                break;
                
            case "4":
                await ImportCustomFileAsync();
                break;
                
            case "5":
                await TestModelAsync();
                break;
                
            default:
                Console.WriteLine("Exiting.");
                break;
        }
    }
    
    private static async Task ImportCustomFileAsync()
    {
        Console.Write("Enter CSV file path: ");
        var path = Console.ReadLine();
        
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Console.WriteLine("∴ File not found");
            return;
        }
        
        var importer = new OpenCellIDImporter();
        var model = new RFLocatorModel();
        
        await importer.ImportToModelAsync(model, path, 100000);
        model.Train();
        
        var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "rf_locator.json");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        model.SaveModel(modelPath);
    }
    
    private static async Task TestModelAsync()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "rf_locator.json");
        
        if (!File.Exists(modelPath))
        {
            Console.WriteLine("∴ No trained model found. Train first!");
            return;
        }
        
        var model = new RFLocatorModel();
        model.LoadModel(modelPath);
        
        Console.WriteLine();
        Console.WriteLine("Enter test measurements (or 'quit' to exit):");
        
        while (true)
        {
            Console.Write("RSRP (dBm, e.g. -85): ");
            var rsrpStr = Console.ReadLine();
            if (rsrpStr == "quit") break;
            
            Console.Write("Cell ID: ");
            var cellIdStr = Console.ReadLine();
            
            if (float.TryParse(rsrpStr, out var rsrp) && long.TryParse(cellIdStr, out var cellId))
            {
                var measurement = new CellTowerMeasurement
                {
                    RSRP = rsrp,
                    RSRQ = -10,
                    RSSI = rsrp + 20,
                    SINR = 10,
                    CellId = cellId,
                    MCC = 310,
                    MNC = 410,
                    TimingAdvance = 10
                };
                
                var prediction = model.Predict(measurement);
                
                Console.WriteLine();
                Console.WriteLine($"  Predicted Location: {prediction.Latitude:F6}, {prediction.Longitude:F6}");
                Console.WriteLine($"  Confidence: {prediction.Confidence:P0}");
                Console.WriteLine($"  Radius: ±{prediction.ConfidenceRadius}m");
                Console.WriteLine();
            }
        }
        
        await Task.CompletedTask;
    }
    
    private static int GetArgValue(string[] args, string key, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key && int.TryParse(args[i + 1], out var value))
                return value;
        }
        return defaultValue;
    }
    
    private static string? GetArgValueString(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key)
                return args[i + 1];
        }
        return null;
    }
}
