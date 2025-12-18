/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    MODEM CONTROLLER - SERIAL INTERFACE                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Controls cellular modems via serial/USB connection using AT commands.     ║
 * ║  Supports Quectel, Telit, Sierra Wireless, and generic modems.            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.IO.Ports;
using System.Text;

namespace NIGHTFRAME.Drone.Cellular;

public class ModemController : IDisposable
{
    private SerialPort? _serialPort;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly string _portName;
    private readonly int _baudRate;
    private bool _isConnected = false;
    
    // Public connection state
    public bool IsConnected => _isConnected && _serialPort?.IsOpen == true;
    
    // Modem identification
    public string Manufacturer { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string FirmwareVersion { get; private set; } = "";
    
    public event Action<CellTowerMeasurement>? OnMeasurementReceived;
    public event Action<string>? OnUnsolicited;
    
    public ModemController(string portName = "COM3", int baudRate = 115200)
    {
        _portName = portName;
        _baudRate = baudRate;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CONNECTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Opens connection to the modem.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            _serialPort = new SerialPort(_portName, _baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 5000,
                WriteTimeout = 5000,
                NewLine = "\r\n"
            };
            
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
            
            // Test connection
            var response = await SendCommandAsync("AT");
            if (!response.Contains("OK"))
            {
                _serialPort.Close();
                return false;
            }
            
            // Get modem info
            await IdentifyModemAsync();
            
            _isConnected = true;
            Console.WriteLine($"◈ Modem connected: {Manufacturer} {Model}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Modem connection failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Closes the modem connection.
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _serialPort?.Close();
        _serialPort?.Dispose();
        Console.WriteLine("◎ Modem disconnected");
    }
    
    private async Task IdentifyModemAsync()
    {
        // Get manufacturer
        var mfrResponse = await SendCommandAsync("AT+CGMI");
        Manufacturer = mfrResponse.Replace("OK", "").Replace("\r", "").Replace("\n", "").Trim();
        
        // Get model
        var modelResponse = await SendCommandAsync("AT+CGMM");
        Model = modelResponse.Replace("OK", "").Replace("\r", "").Replace("\n", "").Trim();
        
        // Get firmware
        var fwResponse = await SendCommandAsync("AT+CGMR");
        FirmwareVersion = fwResponse.Replace("OK", "").Replace("\r", "").Replace("\n", "").Trim();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              COMMAND EXECUTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Sends an AT command and waits for response.
    /// </summary>
    public async Task<string> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            throw new InvalidOperationException("Modem not connected");
        
        await _commandLock.WaitAsync();
        try
        {
            // Clear buffer
            _serialPort.DiscardInBuffer();
            
            // Send command
            _serialPort.WriteLine(command);
            
            // Wait for response
            var response = new StringBuilder();
            var startTime = DateTime.UtcNow;
            
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var data = _serialPort.ReadExisting();
                    response.Append(data);
                    
                    // Check for termination
                    if (data.Contains("OK") || data.Contains("ERROR") || data.Contains(">"))
                        break;
                }
                
                await Task.Delay(50);
            }
            
            return response.ToString();
        }
        finally
        {
            _commandLock.Release();
        }
    }
    
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null) return;
        
        try
        {
            var data = _serialPort.ReadExisting();
            
            // Check for unsolicited responses (URCs)
            if (data.Contains("+CREG:") || data.Contains("+CEREG:") || 
                data.Contains("+CRING:") || data.Contains("+CMTI:"))
            {
                OnUnsolicited?.Invoke(data);
            }
        }
        catch { }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              SIGNAL MEASUREMENT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets current signal quality (basic CSQ).
    /// </summary>
    public async Task<(float rssiDbm, int ber)> GetSignalQualityAsync()
    {
        var response = await SendCommandAsync("AT+CSQ");
        return ATCommandParser.ParseCSQ(response);
    }
    
    /// <summary>
    /// Gets extended signal quality (CESQ).
    /// </summary>
    public async Task<CellTowerMeasurement> GetExtendedSignalAsync()
    {
        var response = await SendCommandAsync("AT+CESQ");
        return ATCommandParser.ParseCESQ(response);
    }
    
    /// <summary>
    /// Gets full cell measurement (Quectel specific).
    /// </summary>
    public async Task<CellTowerMeasurement> GetCellMeasurementAsync()
    {
        // Try Quectel CPSI first
        if (Manufacturer.Contains("Quectel", StringComparison.OrdinalIgnoreCase))
        {
            var response = await SendCommandAsync("AT+CPSI?");
            return ATCommandParser.ParseCPSI(response);
        }
        
        // Fallback to CESQ
        return await GetExtendedSignalAsync();
    }
    
    /// <summary>
    /// Gets serving and neighbor cell information (Quectel engineering mode).
    /// </summary>
    public async Task<(CellTowerMeasurement serving, List<NeighborCell> neighbors)> GetEngineeringModeAsync()
    {
        // Enable engineering mode if needed
        await SendCommandAsync("AT+QENG=\"servingcell\"");
        await Task.Delay(100);
        
        var response = await SendCommandAsync("AT+QENG=\"neighbourcell\"", 10000);
        return ATCommandParser.ParseQENG(response);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              NETWORK OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets current network registration status.
    /// </summary>
    public async Task<(int status, int lac, long cellId)> GetRegistrationAsync()
    {
        // Enable extended registration reports
        await SendCommandAsync("AT+CEREG=2");
        
        var response = await SendCommandAsync("AT+CEREG?");
        return ATCommandParser.ParseCEREG(response);
    }
    
    /// <summary>
    /// Forces manual cell selection.
    /// USE WITH CAUTION - can cause loss of service!
    /// </summary>
    public async Task<bool> ForceCellSelectionAsync(int mcc, int mnc, string radioType = "7")
    {
        // AT+COPS=1,2,"MCC-MNC",AcT
        // AcT: 0=GSM, 2=UMTS, 7=LTE, 12=NR
        var command = $"AT+COPS=1,2,\"{mcc:D3}{mnc:D2}\",{radioType}";
        var response = await SendCommandAsync(command, 30000); // Cell selection takes time
        
        return response.Contains("OK");
    }
    
    /// <summary>
    /// Locks to a specific EARFCN (frequency channel).
    /// Quectel specific.
    /// </summary>
    public async Task<bool> LockFrequencyAsync(int earfcn)
    {
        // AT+QNWLOCK="common/lte",1,earfcn,pci
        var command = $"AT+QNWLOCK=\"common/lte\",1,{earfcn}";
        var response = await SendCommandAsync(command);
        
        return response.Contains("OK");
    }
    
    /// <summary>
    /// Unlocks frequency and returns to automatic selection.
    /// </summary>
    public async Task<bool> UnlockFrequencyAsync()
    {
        var response = await SendCommandAsync("AT+QNWLOCK=\"common/lte\",0");
        return response.Contains("OK");
    }
    
    /// <summary>
    /// Triggers a network rescan.
    /// </summary>
    public async Task<bool> RescanNetworkAsync()
    {
        // Deregister and re-register
        await SendCommandAsync("AT+COPS=2", 5000);
        await Task.Delay(1000);
        var response = await SendCommandAsync("AT+COPS=0", 60000);
        
        return response.Contains("OK");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CONTINUOUS POLLING
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Starts continuous signal polling for neural network training data collection.
    /// </summary>
    public async Task StartPollingAsync(int intervalMs, CancellationToken ct)
    {
        Console.WriteLine($"◈ Starting modem polling every {intervalMs}ms");
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var measurement = await GetCellMeasurementAsync();
                OnMeasurementReceived?.Invoke(measurement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Polling error: {ex.Message}");
            }
            
            await Task.Delay(intervalMs, ct);
        }
    }
    
    /// <summary>
    /// Lists available serial ports.
    /// </summary>
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }
    
    public void Dispose()
    {
        Disconnect();
        _commandLock.Dispose();
    }
}
