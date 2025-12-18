/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    ANDROID MAIN ACTIVITY                                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Entry point for the Android Scout app.                                    ║
 * ║  Requests permissions and starts background services.                      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using NIGHTFRAME.Drone.Android.Cellular;

namespace NIGHTFRAME.Drone.Android;

[Activity(
    Label = "NFRAME Scout", 
    MainLauncher = true,
    Theme = "@style/AppTheme",
    LaunchMode = LaunchMode.SingleTop)]
public class MainActivity : AppCompatActivity
{
    private const int PermissionRequestCode = 1000;
    
    // Required permissions for full functionality
    private static readonly string[] RequiredPermissions = new[]
    {
        // Location
        global::Android.Manifest.Permission.AccessFineLocation,
        global::Android.Manifest.Permission.AccessCoarseLocation,
        
        // WiFi
        global::Android.Manifest.Permission.AccessWifiState,
        global::Android.Manifest.Permission.ChangeWifiState,
        
        // Cellular/Telephony
        global::Android.Manifest.Permission.ReadPhoneState,
        
        // Network
        global::Android.Manifest.Permission.Internet,
        
        // Background service
        global::Android.Manifest.Permission.ForegroundService
    };
    
    // Additional permissions for API 29+
    private static readonly string[] Api29Permissions = new[]
    {
        "android.permission.READ_PRECISE_PHONE_STATE"
    };
    
    // UI elements
    private TextView? _statusText;
    private TextView? _cellularInfoText;
    private LinearLayout? _statsContainer;
    
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(CreateLayout());
        
        CheckAndRequestPermissions();
    }
    
    private LinearLayout CreateLayout()
    {
        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MatchParent,
                LinearLayout.LayoutParams.MatchParent)
        };
        layout.SetPadding(48, 100, 48, 48);
        layout.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#0a0a0f"));
        
        // Logo
        var logo = new TextView(this)
        {
            Text = "◈",
            TextSize = 80,
            Gravity = global::Android.Views.GravityFlags.Center
        };
        logo.SetTextColor(global::Android.Graphics.Color.ParseColor("#7c3aed"));
        layout.AddView(logo);
        
        // Title
        var title = new TextView(this)
        {
            Text = "NFRAME Scout",
            TextSize = 28,
            Gravity = global::Android.Views.GravityFlags.Center
        };
        title.SetTextColor(global::Android.Graphics.Color.White);
        title.SetPadding(0, 32, 0, 16);
        layout.AddView(title);
        
        // Status
        _statusText = new TextView(this)
        {
            Text = "Initializing...",
            TextSize = 14,
            Gravity = global::Android.Views.GravityFlags.Center
        };
        _statusText.SetTextColor(global::Android.Graphics.Color.ParseColor("#94a3b8"));
        layout.AddView(_statusText);
        
        // Cellular info section
        var cellularHeader = new TextView(this)
        {
            Text = "Cellular Status",
            TextSize = 18,
            Gravity = global::Android.Views.GravityFlags.Left
        };
        cellularHeader.SetTextColor(global::Android.Graphics.Color.ParseColor("#7c3aed"));
        cellularHeader.SetPadding(0, 48, 0, 16);
        layout.AddView(cellularHeader);
        
        _cellularInfoText = new TextView(this)
        {
            Text = "Waiting for cellular data...",
            TextSize = 14
        };
        _cellularInfoText.SetTextColor(global::Android.Graphics.Color.ParseColor("#e2e8f0"));
        layout.AddView(_cellularInfoText);
        
        // Stats container
        _statsContainer = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MatchParent,
                LinearLayout.LayoutParams.WrapContent)
        };
        _statsContainer.SetPadding(0, 32, 0, 0);
        
        AddStatRow(_statsContainer, "Wi-Fi Networks:", "0");
        AddStatRow(_statsContainer, "Mesh Nodes:", "0");
        AddStatRow(_statsContainer, "Cell Measurements:", "0");
        AddStatRow(_statsContainer, "Last Update:", "Never");
        
        layout.AddView(_statsContainer);
        
        // Control buttons
        var buttonContainer = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MatchParent,
                LinearLayout.LayoutParams.WrapContent)
        };
        buttonContainer.SetPadding(0, 48, 0, 0);
        
        var exportButton = new Button(this)
        {
            Text = "Export Training Data",
            LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1)
        };
        exportButton.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#7c3aed"));
        exportButton.SetTextColor(global::Android.Graphics.Color.White);
        exportButton.Click += OnExportButtonClick;
        buttonContainer.AddView(exportButton);
        
        layout.AddView(buttonContainer);
        
        return layout;
    }
    
    private void AddStatRow(LinearLayout container, string label, string value)
    {
        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MatchParent,
                LinearLayout.LayoutParams.WrapContent)
        };
        row.SetPadding(0, 16, 0, 16);
        
        var labelView = new TextView(this)
        {
            Text = label,
            TextSize = 16,
            LayoutParameters = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WrapContent, 1)
        };
        labelView.SetTextColor(global::Android.Graphics.Color.ParseColor("#6b7280"));
        row.AddView(labelView);
        
        var valueView = new TextView(this)
        {
            Text = value,
            TextSize = 16
        };
        valueView.SetTextColor(global::Android.Graphics.Color.White);
        row.AddView(valueView);
        
        container.AddView(row);
    }
    
    private void CheckAndRequestPermissions()
    {
        var permissionsToRequest = RequiredPermissions
            .Where(p => ContextCompat.CheckSelfPermission(this, p) != Permission.Granted)
            .ToList();
        
        // Add API 29+ permissions
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            foreach (var perm in Api29Permissions)
            {
                if (ContextCompat.CheckSelfPermission(this, perm) != Permission.Granted)
                {
                    permissionsToRequest.Add(perm);
                }
            }
        }
        
        if (permissionsToRequest.Count > 0)
        {
            _statusText?.SetText("Requesting permissions...", TextView.BufferType.Normal);
            ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), PermissionRequestCode);
        }
        else
        {
            StartScoutServices();
        }
    }
    
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        
        if (requestCode == PermissionRequestCode)
        {
            var allGranted = grantResults.All(r => r == Permission.Granted);
            var criticalGranted = grantResults.Length >= 3 && 
                                  grantResults.Take(3).All(r => r == Permission.Granted);
            
            if (allGranted)
            {
                StartScoutServices();
            }
            else if (criticalGranted)
            {
                // Start with limited functionality
                _statusText?.SetText("Some permissions denied - limited functionality", TextView.BufferType.Normal);
                StartScoutServices();
            }
            else
            {
                Toast.MakeText(this, "Location permissions required for mesh scanning", ToastLength.Long)?.Show();
                _statusText?.SetText("Permissions denied", TextView.BufferType.Normal);
            }
        }
    }
    
    private void StartScoutServices()
    {
        // Start Wi-Fi Scout Service
        var wifiServiceIntent = new Intent(this, typeof(ScoutService));
        
        // Start Cellular Scout Service
        var cellularServiceIntent = new Intent(this, typeof(CellularScoutService));
        
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            StartForegroundService(wifiServiceIntent);
            StartForegroundService(cellularServiceIntent);
        }
        else
        {
            StartService(wifiServiceIntent);
            StartService(cellularServiceIntent);
        }
        
        _statusText?.SetText("Scout services active", TextView.BufferType.Normal);
        Toast.MakeText(this, "Wi-Fi and Cellular scouts started", ToastLength.Short)?.Show();
        
        // Update UI periodically
        var updateTimer = new Timer(_ => RunOnUiThread(UpdateUI), null, 
            TimeSpan.FromSeconds(2), 
            TimeSpan.FromSeconds(5));
    }
    
    private void UpdateUI()
    {
        // This would retrieve data from the bound services
        _cellularInfoText?.SetText(
            "Technology: Checking...\n" +
            "Signal: --\n" +
            "Cell ID: --",
            TextView.BufferType.Normal);
    }
    
    private void OnExportButtonClick(object? sender, EventArgs e)
    {
        Toast.MakeText(this, "Exporting training data...", ToastLength.Short)?.Show();
        
        // TODO: Bind to CellularScoutService and call ExportTrainingData()
        // Then save to file or send to Orchestrator
    }
}
