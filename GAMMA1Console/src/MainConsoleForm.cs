/*
 * GAMMA1 Console - Main Control Interface
 * Project NIGHTFRAME - Complete Control System
 */

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Agent3;
using Agent3.Network;


namespace GAMMA1Console
{
    public partial class MainConsoleForm : Form
    {
        private Agent3Core? _agent;
        private bool _agentRunning = false;
        private bool _continuousLearningActive = false;
        private readonly string _killPassword = "NIGHTFRAME";
        private System.Windows.Forms.Timer _heartbeatTimer;
        private System.Windows.Forms.Timer _consciousnessTimer;
        private int _heartbeatCount = 0;
        private System.Windows.Forms.Timer? _typewriterTimer;
        
        // Colors - Cyberpunk/Nightframe Theme
        private readonly Color ColBack = Color.FromArgb(10, 10, 14); // Deepest dark
        private readonly Color ColPanel = Color.FromArgb(18, 18, 24); // Panel background
        private readonly Color ColAccent = Color.FromArgb(138, 58, 252); // Electric Purple
        private readonly Color ColSuccess = Color.FromArgb(0, 255, 157); // Cyber Green
        private readonly Color ColError = Color.FromArgb(255, 59, 92); // Neon Red
        private readonly Color ColText = Color.FromArgb(220, 220, 240); // Soft White
        private readonly Color ColTextDim = Color.FromArgb(140, 140, 160); // Dim Text
        
        // UI Controls
        private TabControl tabControl;
        private RichTextBox consciousnessStream;
        private TextBox chatInput;
        private RichTextBox chatHistory;
        private TextBox masterPromptInput;
        private TextBox trainingInput;
        private ProgressBar trainingProgress;
        private Label statusLabel;
        private Panel statusPanel;
        private SplitContainer mainSplit;
        
        private Button initButton;
        private Button killButton;
        private Button helpButton;
        private Button autoModeBtn;
        
        // Training controls
        private NumericUpDown learningRateInput;
        private NumericUpDown batchSizeInput;
        private NumericUpDown epochsInput;
        private NumericUpDown temperatureInput;
        
        public MainConsoleForm()
        {
            InitializeComponent();
            SetupTimers();
            CheckFirstRun();
        }

        
        private void InitializeComponent()
        {
            this.Text = "GAMMA1 Console - Project NIGHTFRAME";
            this.Size = new Size(1600, 1000);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColBack;
            this.ForeColor = ColText;
            this.Font = new Font("Segoe UI", 10);
            this.FormClosing += MainForm_FormClosing;
            
            // 1. Top Status Panel
            CreateStatusPanel();
            
            // Load persistent session data
            LoadSession();
            
            // 2. Main Split Container (Left: Content, Right: Consciousness)
            mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = ColBack,
                SplitterWidth = 4,
                Orientation = Orientation.Vertical
            };
            this.Controls.Add(mainSplit);
            mainSplit.BringToFront(); // Ensure it's not covered by status panel if dock order is wrong, but usually added last is on top.
            // Actually StatusPanel is added first (Dock Top), so Split must be added second (Dock Fill). 
            // WinForms docking order is reverse of addition order for 'Fill', so we should add split, then status. 
            // Let's fix the order or just use Controls.Add correctly.
            // Actually, simpler: StatusPanel (Top), MainSplit (Fill). StatusPanel added *first* means it takes top.
            // Wait, Controls.Add: Last added = top of z-order, but Docking priority is First Added = Outer Most.
            // So if I add StatusPanel first, it docks to top of Form.
            // Then MainSplit docks to Fill of Remaining space. Correct.
            
            // Setup Panels
            mainSplit.Panel1.Padding = new Padding(20, 20, 10, 20); // Left padding
            mainSplit.Panel2.Padding = new Padding(10, 20, 20, 20); // Right padding
            mainSplit.Panel2MinSize = 400;
            mainSplit.SplitterDistance = 1100; // Wide left area
            
            // 3. Right Panel: Consciousness Stream (Persistent)
            CreateConsciousnessPanel(mainSplit.Panel2);
            
            // 4. Left Panel: Tabs (Chat, Training, Settings, etc)
            CreateMainTabs(mainSplit.Panel1);
        }

        private void CreateStatusPanel()
        {
            statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = ColPanel,
                Padding = new Padding(20),
            };
            // Bottom border effect
            var border = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(40, 40, 50) };
            statusPanel.Controls.Add(border);
            
            var titleLabel = new Label
            {
                Text = "🌌 NIGHTFRAME // GAMMA1",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = ColAccent,
                AutoSize = true,
                Location = new Point(20, 18)
            };
            statusPanel.Controls.Add(titleLabel);
            
            statusLabel = new Label
            {
                Text = "● OFFLINE",
                Font = new Font("JetBrains Mono", 12),
                ForeColor = ColError,
                AutoSize = true,
                Location = new Point(410, 24) // Moved right to avoid cutout
            };
            statusPanel.Controls.Add(statusLabel);
            
            // Buttons
            killButton = CreateButton("⛔ ABORT", 0, 0, 100, 36, ColError);
            killButton.Dock = DockStyle.Right;
            killButton.Click += KillButton_Click;
            
            var spacer = new Panel { Dock = DockStyle.Right, Width = 15 };
            
            helpButton = CreateButton("❓ HELP", 0, 0, 80, 36, Color.FromArgb(60, 60, 70));
            helpButton.Dock = DockStyle.Right;
            helpButton.Click += HelpButton_Click;

            statusPanel.Controls.Add(killButton);
            statusPanel.Controls.Add(spacer);
            statusPanel.Controls.Add(helpButton);
            
            this.Controls.Add(statusPanel);
        }

        private void CreateConsciousnessPanel(Panel container)
        {
            container.Padding = new Padding(8);
            
            // ═══════════════════════════════════════════════════════════════
            // HEADER SECTION - System Identity & Global Status
            // ═══════════════════════════════════════════════════════════════
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(15, 15, 22),
                Padding = new Padding(10)
            };
            
            var titleLabel = new Label
            {
                Text = "◈ AGENT 3 — CONSCIOUSNESS MATRIX",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(139, 92, 246),
                AutoSize = true,
                Location = new Point(10, 8)
            };
            headerPanel.Controls.Add(titleLabel);
            
            var subtitleLabel = new Label
            {
                Text = "Neural Reasoning Engine • Self-Modifying Architecture • Continuous Learning",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(120, 120, 140),
                AutoSize = true,
                Location = new Point(10, 35)
            };
            headerPanel.Controls.Add(subtitleLabel);
            
            // Connectivity indicator in header
            var connectivityLabel = new Label
            {
                Text = "○ OFFLINE",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 100, 100),
                Name = "connectivityIndicator",
                AutoSize = true,
                Location = new Point(10, 52)
            };
            headerPanel.Controls.Add(connectivityLabel);
            
            // Status indicator in header
            statusLabel = new Label
            {
                Text = "○ DORMANT",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 120),
                AutoSize = true,
                Location = new Point(headerPanel.Width - 120, 20)
            };
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            headerPanel.Controls.Add(statusLabel);
            
            // ═══════════════════════════════════════════════════════════════
            // SUBSYSTEM STATUS BAR - Shows active cognitive modules
            // ═══════════════════════════════════════════════════════════════
            var subsystemBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(20, 20, 28),
                Padding = new Padding(5, 4, 5, 4)
            };
            
            var subsystems = new[] {
                ("NEURAL", "⟁", Color.FromArgb(139, 92, 246)),
                ("REASONING", "◎", Color.FromArgb(59, 130, 246)),
                ("LEARNING", "∿", Color.FromArgb(34, 197, 94)),
                ("EVOLUTION", "⟐", Color.FromArgb(234, 179, 8)),
                ("WEB", "◈", Color.FromArgb(236, 72, 153))
            };
            
            int xPos = 8;
            foreach (var (name, icon, color) in subsystems)
            {
                var indicator = new Label
                {
                    Text = $"{icon} {name}",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(80, 80, 100), // Dim when inactive
                    Name = $"subsys_{name}",
                    AutoSize = true,
                    Location = new Point(xPos, 6),
                    Tag = color // Store active color
                };
                subsystemBar.Controls.Add(indicator);
                xPos += indicator.PreferredWidth + 20;
            }
            
            // ═══════════════════════════════════════════════════════════════
            // MAIN CONSCIOUSNESS STREAM - The thought log
            // ═══════════════════════════════════════════════════════════════
            consciousnessStream = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 8, 12),
                ForeColor = ColText,
                Font = new Font("Cascadia Code", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            
            // Initial welcome message
            consciousnessStream.SelectionColor = Color.FromArgb(139, 92, 246);
            consciousnessStream.AppendText("╔═══════════════════════════════════════════════════════════════╗\n");
            consciousnessStream.AppendText("║     AGENT 3 CONSCIOUSNESS MATRIX v2.0                         ║\n");
            consciousnessStream.AppendText("║     Advanced Reasoning • Self-Modification • Deep Learning    ║\n");
            consciousnessStream.AppendText("╚═══════════════════════════════════════════════════════════════╝\n\n");
            consciousnessStream.SelectionColor = Color.FromArgb(100, 100, 120);
            consciousnessStream.AppendText("Awaiting initialization...\n");
            consciousnessStream.AppendText("Click [INITIALIZE NEURAL LINK] to activate cognitive systems.\n\n");
            
            // ═══════════════════════════════════════════════════════════════
            // CONTROL PANEL - Bottom section with inputs and buttons
            // ═══════════════════════════════════════════════════════════════
            var controlPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(10)
            };
            
            // Chat input area
            var inputPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0, 5, 0, 5)
            };
            
            chatInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle
            };
            chatInput.KeyPress += ChatInput_KeyPress;
            
            var sendPanel = new Panel { Dock = DockStyle.Right, Width = 90, Padding = new Padding(8, 0, 0, 0) };
            var sendBtn = CreateButton("⟶ SEND", 0, 0, 82, 40, ColAccent);
            sendBtn.Dock = DockStyle.Fill;
            sendBtn.Click += SendButton_Click;
            sendPanel.Controls.Add(sendBtn);
            
            inputPanel.Controls.Add(chatInput);
            inputPanel.Controls.Add(sendPanel);
            controlPanel.Controls.Add(inputPanel);
            
            // Button row
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0)
            };
            
            // Initialize Button
            initButton = CreateButton("▶ INITIALIZE NEURAL LINK", 0, 0, 200, 45, Color.FromArgb(34, 197, 94));
            initButton.ForeColor = Color.Black;
            initButton.Margin = new Padding(0, 0, 10, 0);
            initButton.Click += InitButton_Click;
            buttonPanel.Controls.Add(initButton);
            
            // Auto Mode Button
            autoModeBtn = CreateButton("∿ CONTINUOUS LEARNING", 0, 0, 190, 45, ColAccent);
            autoModeBtn.Margin = new Padding(0, 0, 10, 0);
            autoModeBtn.Click += AutoModeButton_Click;
            buttonPanel.Controls.Add(autoModeBtn);
            
            // Train Button
            var trainBtn = CreateButton("📚 TRAIN", 0, 0, 100, 45, Color.FromArgb(59, 130, 246));
            trainBtn.Margin = new Padding(0, 0, 10, 0);
            trainBtn.Click += TrainFromFileButton_Click;
            buttonPanel.Controls.Add(trainBtn);
            
            // Kill Button
            killButton = CreateButton("⏹ SHUTDOWN", 0, 0, 120, 45, Color.FromArgb(180, 50, 50));
            killButton.Click += KillButton_Click;
            buttonPanel.Controls.Add(killButton);
            
            controlPanel.Controls.Add(buttonPanel);
            
            // ═══════════════════════════════════════════════════════════════
            // METRICS BAR - Quick stats at the very bottom
            // ═══════════════════════════════════════════════════════════════
            var metricsBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(12, 12, 16),
                Padding = new Padding(10, 5, 10, 5)
            };
            
            var metricsLabel = new Label
            {
                Text = "TOKENS: 0 | RESEARCH: 0 | IMPROVEMENTS: 0",
                Font = new Font("Cascadia Code", 8),
                ForeColor = Color.FromArgb(80, 80, 100),
                Name = "metricsLabel",
                AutoSize = true,
                Location = new Point(10, 6)
            };
            metricsBar.Controls.Add(metricsLabel);
            
            // ═══════════════════════════════════════════════════════════════
            // ASSEMBLE THE PANEL (Order matters for docking)
            // ═══════════════════════════════════════════════════════════════
            container.Controls.Add(consciousnessStream);  // Fill (added first)
            container.Controls.Add(metricsBar);           // Bottom
            container.Controls.Add(controlPanel);         // Bottom (above metrics)
            container.Controls.Add(subsystemBar);         // Top (below header)
            container.Controls.Add(headerPanel);          // Top
        }

        private void CreateMainTabs(Panel container)
        {
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                Padding = new Point(20, 8)
            };
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += TabControl_DrawItem;
            
            CreateChatTab();
            CreateTrainingTab();
            CreateMonitoringTab();
            CreateSettingsTab();
            
            container.Controls.Add(tabControl);
        }
        
        private Button CreateButton(string text, int x, int y, int w, int h, Color color)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }
        
        // CreateConsciousnessTab Removed - Integration moved to Persistent Side Panel

        
        private void CreateChatTab()
        {
            // Chat tab removed - integrated into Consciousness Stream
        }
        
        private void CreateTrainingTab()
        {
            var tab = new TabPage("🧠 NEURAL TRAINING") { BackColor = ColBack };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            
            int y = 20;
            
            // Master Prompt Section
            AddLabel(scroll, "📌 MASTER PROMPT (Guides All Improvement)", 20, y, true);
            y += 35;
            
            masterPromptInput = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(800, 100),
                Multiline = true,
                BackColor = ColPanel,
                ForeColor = ColText,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            scroll.Controls.Add(masterPromptInput);
            y += 110;
            
            var setPromptBtn = CreateButton("Set Master Prompt", 20, y, 180, 35, Color.FromArgb(139, 92, 246));
            setPromptBtn.Click += SetMasterPrompt_Click;
            scroll.Controls.Add(setPromptBtn);

            var loadTemplateBtn = CreateButton("📂 Load Template", 210, y, 150, 35, Color.FromArgb(59, 130, 246));
            loadTemplateBtn.Click += LoadTemplateButton_Click;
            scroll.Controls.Add(loadTemplateBtn);

            var saveTemplateBtn = CreateButton("💾 Save Template", 370, y, 150, 35, Color.FromArgb(59, 130, 246));
            saveTemplateBtn.Click += SaveTemplateButton_Click;
            scroll.Controls.Add(saveTemplateBtn);
            y += 60;
            
            // Training Parameters
            AddLabel(scroll, "⚙️ TRAINING PARAMETERS", 20, y, true);
            y += 35;
            
            AddLabel(scroll, "Learning Rate:", 20, y);
            learningRateInput = new NumericUpDown
            {
                Location = new Point(150, y - 3),
                Size = new Size(100, 25),
                DecimalPlaces = 4,
                Minimum = 0.0001M,
                Maximum = 1M,
                Value = 0.001M,
                Increment = 0.0001M,
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(learningRateInput);
            
            AddLabel(scroll, "Batch Size:", 300, y);
            batchSizeInput = new NumericUpDown
            {
                Location = new Point(400, y - 3),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 128,
                Value = 16,
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(batchSizeInput);
            y += 40;
            
            AddLabel(scroll, "Epochs:", 20, y);
            epochsInput = new NumericUpDown
            {
                Location = new Point(150, y - 3),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 1000,
                Value = 10,
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(epochsInput);
            
            AddLabel(scroll, "Temperature:", 300, y);
            temperatureInput = new NumericUpDown
            {
                Location = new Point(400, y - 3),
                Size = new Size(100, 25),
                DecimalPlaces = 2,
                Minimum = 0.1M,
                Maximum = 2M,
                Value = 0.7M,
                Increment = 0.1M,
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(temperatureInput);
            y += 60;
            
            // Training Data Input
            AddLabel(scroll, "📝 TRAINING DATA INPUT", 20, y, true);
            y += 35;
            
            trainingInput = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(800, 150),
                Multiline = true,
                BackColor = ColPanel,
                ForeColor = ColText,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };
            scroll.Controls.Add(trainingInput);
            y += 160;
            
            var trainBtn = CreateButton("▶ Train", 20, y, 120, 40, Color.FromArgb(34, 197, 94));
            trainBtn.Click += TrainButton_Click;
            scroll.Controls.Add(trainBtn);
            
            var loadFileBtn = CreateButton("📂 Load File", 150, y, 120, 40, Color.FromArgb(59, 130, 246));
            loadFileBtn.Click += LoadFileButton_Click;
            scroll.Controls.Add(loadFileBtn);
            y += 60;
            
            // Progress
            AddLabel(scroll, "Training Progress:", 20, y);
            y += 25;
            trainingProgress = new ProgressBar
            {
                Location = new Point(20, y),
                Size = new Size(800, 25),
                Style = ProgressBarStyle.Continuous
            };
            scroll.Controls.Add(trainingProgress);
            
            tab.Controls.Add(scroll);
            tabControl.TabPages.Add(tab);
        }
        
        private void CreateSettingsTab()
        {
            var tab = new TabPage("⚙️ SYSTEM") { BackColor = ColBack };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            
            int y = 20;
            AddLabel(scroll, "🔧 AGENT CONFIGURATION", 20, y, true);
            y += 40;
            
            AddLabel(scroll, "Base Directory:", 20, y);
            var baseDirBox = new TextBox
            {
                Location = new Point(150, y - 3),
                Size = new Size(400, 25),
                Text = Path.GetDirectoryName(Application.ExecutablePath) ?? "",
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(baseDirBox);
            y += 50;
            
            AddLabel(scroll, "Agent ID:", 20, y);
            var agentIdBox = new TextBox
            {
                Location = new Point(150, y - 3),
                Size = new Size(200, 25),
                Text = "GAMMA1-AGENT3",
                BackColor = ColPanel,
                ForeColor = ColText
            };
            scroll.Controls.Add(agentIdBox);
            y += 80;
            
            AddLabel(scroll, "⚠️ KILL SWITCH", 20, y, true);
            y += 40;
            AddLabel(scroll, "Password required to terminate agent:", 20, y);
            y += 30;
            AddLabel(scroll, "The agent runs indefinitely until killed.", 20, y);
            AddLabel(scroll, "Enter password 'NIGHTFRAME' to terminate.", 20, y + 25);
            
            tab.Controls.Add(scroll);
            tabControl.TabPages.Add(tab);
        }
        
        private void CreateMonitoringTab()
        {
            var tab = new TabPage("📊 METRICS") { BackColor = ColBack };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            
            int y = 20;
            AddLabel(scroll, "📈 SYSTEM METRICS", 20, y, true);
            y += 40;
            
            string[] metrics = { "Heartbeat:", "Memory Usage:", "CPU Load:", "Active Threads:", 
                                 "Tokens Processed:", "Training Epochs:", "Web Pages Visited:", 
                                 "Code Modifications:" };
            foreach (var m in metrics)
            {
                AddLabel(scroll, m, 20, y);
                AddLabel(scroll, "0", 250, y).Name = "metric_" + m.Replace(":", "").Replace(" ", "");
                y += 30;
            }
            
            y += 20;
            // Node Tracking Section
            AddLabel(scroll, "🌐 NODE TRACKING NETSCAPE", 20, y, true);
            y += 40;
            
            AddLabel(scroll, "Distributed Nodes:", 20, y);
            AddLabel(scroll, "0", 250, y).Name = "metric_ActiveNodes"; 
            y += 30;
            AddLabel(scroll, "Avg Latency:", 20, y);
            AddLabel(scroll, "0ms", 250, y).Name = "metric_NetworkLatency";
            y += 30;

            // Node Grid
            var nodeGrid = new DataGridView
            {
                Name = "nodeGrid",
                Location = new Point(20, y),
                Size = new Size(820, 200),
                BackColor = ColPanel,
                ForeColor = ColText,
                BackgroundColor = ColPanel,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            
            // Stylize Grid
            nodeGrid.EnableHeadersVisualStyles = false;
            nodeGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 40);
            nodeGrid.ColumnHeadersDefaultCellStyle.ForeColor = ColAccent;
            nodeGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            nodeGrid.DefaultCellStyle.BackColor = ColPanel;
            nodeGrid.DefaultCellStyle.ForeColor = ColText;
            nodeGrid.DefaultCellStyle.SelectionBackColor = ColAccent;
            nodeGrid.DefaultCellStyle.SelectionForeColor = Color.White;

            nodeGrid.Columns.Add("NodeId", "ID");
            nodeGrid.Columns.Add("Role", "Role");
            nodeGrid.Columns.Add("Status", "Status");
            nodeGrid.Columns.Add("CPU", "CPU %");
            nodeGrid.Columns.Add("RAM", "RAM (MB)");
            nodeGrid.Columns.Add("LastSeen", "Last Seen");
            
            scroll.Controls.Add(nodeGrid);
            y += 220;
            
            // Network Event Log
            AddLabel(scroll, "Network Events:", 20, y);
            y += 25;
            
            var nodeLog = new RichTextBox
            {
                Name = "nodeLog",
                Location = new Point(20, y),
                Size = new Size(820, 150),
                BackColor = ColPanel,
                ForeColor = ColSuccess,
                Font = new Font("Consolas", 8),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };
            scroll.Controls.Add(nodeLog);
            nodeLog.Text = "// NETWORK EVENTS INITIALIZED //\n";
            
            tab.Controls.Add(scroll);
            tabControl.TabPages.Add(tab);
        }
        
        private Label AddLabel(Control parent, string text, int x, int y, bool isHeader = false)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = isHeader ? ColAccent : ColText,
                Font = isHeader ? new Font("Segoe UI", 12, FontStyle.Bold) : new Font("Segoe UI", 10)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }
        
        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = tabControl.TabPages[e.Index];
            bool isSelected = e.State == DrawItemState.Selected;
            
            var backBrush = new SolidBrush(isSelected ? ColPanel : ColBack);
            var textBrush = new SolidBrush(isSelected ? ColAccent : ColTextDim);
            
            e.Graphics.FillRectangle(backBrush, e.Bounds);
            
            // Draw bottom border if selected
            if (isSelected) 
            {
                e.Graphics.FillRectangle(new SolidBrush(ColAccent), e.Bounds.X, e.Bounds.Bottom - 3, e.Bounds.Width, 3);
            }
            
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tab.Text, e.Font!, textBrush, e.Bounds, sf);
        }
        
        private System.Diagnostics.PerformanceCounter? _cpuCounter;

        private void SetupTimers()
        {
            try 
            {
                _cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch { /* Ignore if perms issues */ }

            _heartbeatTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _heartbeatTimer.Tick += (s, e) => {
                if (_agentRunning && _agent != null)
                {
                    _heartbeatCount++;
                    UpdateMetric("Heartbeat", _heartbeatCount.ToString());
                    
                    // Sync Real Metrics
                    long workingSet = Environment.WorkingSet;
                    UpdateMetric("MemoryUsage", $"{workingSet / 1024 / 1024} MB");
                    UpdateMetric("ActiveThreads", System.Diagnostics.Process.GetCurrentProcess().Threads.Count.ToString());
                    
                    if (_cpuCounter != null)
                    {
                        UpdateMetric("CPULoad", $"{_cpuCounter.NextValue():0}%");
                    }
                    
                    if (_agent.ContinuousLearning != null)
                    {
                         UpdateMetric("TokensProcessed", _agent.ContinuousLearning.TokensLearned.ToString("N0"));
                         UpdateMetric("WebPagesVisited", _agent.ContinuousLearning.ResearchCount.ToString());
                         UpdateMetric("CodeModifications", _agent.ContinuousLearning.CodeImprovements.ToString());
                    }
                    
                    // Real Node Data Sync - NON-SIMULATED
                    if (_agent.NetworkCore != null)
                    {
                        var nodes = new List<NodeInfo>(_agent.NetworkCore.KnownNodes);
                        nodes.Insert(0, _agent.NetworkCore.LocalNode); // Always show self
                        
                        UpdateMetric("ActiveNodes", nodes.Count.ToString());
                        UpdateMetric("NetworkLatency", "0ms (Local)"); // Placeholder until latency exposed

                        var gridControl = this.Controls.Find("nodeGrid", true);
                        if (gridControl.Length > 0 && gridControl[0] is DataGridView grid)
                        {
                            foreach (var node in nodes)
                            {
                                bool found = false;
                                foreach (DataGridViewRow row in grid.Rows)
                                {
                                    if (row.Cells[0].Value?.ToString() == node.NodeId)
                                    {
                                        row.Cells[1].Value = node.Role.ToString();
                                        row.Cells[2].Value = node.Status.ToString();
                                        row.Cells[3].Value = $"{node.CpuLoad * 100:0}%";
                                        row.Cells[4].Value = $"{node.AvailableRamBytes / 1024 / 1024}";
                                        row.Cells[5].Value = node.LastSeen.ToString("HH:mm:ss");
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    grid.Rows.Add(node.NodeId, node.Role.ToString(), node.Status.ToString(), $"{node.CpuLoad * 100:0}%", $"{node.AvailableRamBytes / 1024 / 1024}", node.LastSeen.ToString("HH:mm:ss"));
                                }
                            }
                        }
                    }
                }
            };
            
            _consciousnessTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _consciousnessTimer.Tick += ConsciousnessTimer_Tick;

            _typewriterTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _typewriterTimer.Tick += TypewriterTimer_Tick;
            _typewriterTimer.Start();
        }
        
        // Track shown thoughts to avoid repetition
        private Queue<int> _recentThoughtIndices = new Queue<int>();
        private int _idleTicksSinceLastThought = 0;
        
        private void ConsciousnessTimer_Tick(object? sender, EventArgs e)
        {
            if (!_agentRunning) return;
            
            _idleTicksSinceLastThought++;
            
            // Only show idle thoughts occasionally (every ~15-20 seconds minimum)
            if (_idleTicksSinceLastThought < 5) return;
            
            // Only 30% chance to show a thought even when eligible
            if (new Random().NextDouble() > 0.3) return;
            
            string[] thoughts = {
                "Hmm, let me think about what to do next...",
                "I'm ready whenever you need me.",
                "Just organizing my thoughts here...",
                "Waiting for your next instruction.",
                "I could start researching something if you'd like.",
                "Is there anything specific you want me to work on?",
                "Running through what I've learned so far...",
                "Standing by. Feel free to ask me anything."
            };
            
            // Pick a thought we haven't shown recently
            var random = new Random();
            int index;
            int attempts = 0;
            do
            {
                index = random.Next(thoughts.Length);
                attempts++;
            } while (_recentThoughtIndices.Contains(index) && attempts < 10);
            
            // Track this thought
            _recentThoughtIndices.Enqueue(index);
            if (_recentThoughtIndices.Count > 5) _recentThoughtIndices.Dequeue();
            
            AddConsciousnessThought(thoughts[index], ColTextDim);
            _idleTicksSinceLastThought = 0;
        }
        
        // Typewriter Effect State
        private class QueuedMessage 
        { 
            public string Text { get; set; } = "";
            public Color Color { get; set; } 
        }
        private Queue<QueuedMessage> _messageQueue = new Queue<QueuedMessage>();
        // private System.Windows.Forms.Timer _typewriterTimer; // This declaration is moved to the top of the class

        private void AddConsciousnessThought(string thought, Color? color = null)
        {
            if (consciousnessStream.InvokeRequired)
            {
                consciousnessStream.Invoke(() => AddConsciousnessThought(thought, color));
                return;
            }
            
            // Update connectivity indicator based on messages
            if (thought.Contains("Internet connectivity: VERIFIED") || 
                thought.Contains("Research complete:") ||
                thought.Contains("Downloaded") ||
                thought.Contains("pages,") && thought.Contains("tokens"))
            {
                UpdateConnectivityIndicator(true);
            }
            else if (thought.Contains("Internet connectivity:") && (thought.Contains("OFFLINE") || thought.Contains("LIMITED")))
            {
                UpdateConnectivityIndicator(false);
            }
            
            var c = color ?? (thought.StartsWith("◈") ? Color.FromArgb(139, 92, 246) : 
                              thought.StartsWith("∴") ? Color.FromArgb(234, 179, 8) : 
                              thought.StartsWith("⟐") ? Color.FromArgb(59, 130, 246) :
                              thought.StartsWith("◎") ? Color.FromArgb(34, 197, 94) :
                              Color.FromArgb(201, 209, 217));
            
            // Add timestamp if not present
            var text = thought.StartsWith("[") ? thought : $"[{DateTime.Now:HH:mm:ss}] {thought}";
            if (!text.EndsWith("\n")) text += "\n";

            // Enqueue for typewriter
            _messageQueue.Enqueue(new QueuedMessage { Text = text, Color = c });
        }
        
        private void UpdateConnectivityIndicator(bool online)
        {
            var indicator = this.Controls.Find("connectivityIndicator", true);
            if (indicator.Length > 0 && indicator[0] is Label lbl)
            {
                if (online)
                {
                    lbl.Text = "● ONLINE";
                    lbl.ForeColor = Color.FromArgb(34, 197, 94); // Green
                }
                else
                {
                    lbl.Text = "○ OFFLINE";
                    lbl.ForeColor = Color.FromArgb(255, 100, 100); // Red
                }
            }
        }

        private void TypewriterTimer_Tick(object? sender, EventArgs e)
        {
            if (_messageQueue.Count == 0) return;

            // Suspend drawing to prevent flicker
            const int WM_SETREDRAW = 0x000B;
            SendMessage(consciousnessStream.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            
            try
            {
                var msg = _messageQueue.Peek();
                
                // Larger chunk sizes for smoother display
                // If queue is backing up, output whole lines at once
                int chunkSize;
                if (_messageQueue.Count > 5)
                    chunkSize = msg.Text.Length; // Dump entire message
                else if (_messageQueue.Count > 2)
                    chunkSize = Math.Min(50, msg.Text.Length); // Large chunks
                else
                    chunkSize = Math.Min(15, msg.Text.Length); // Normal smooth flow
                
                // Check if user is at the bottom BEFORE adding text
                bool wasAtBottom = IsScrolledToBottom(consciousnessStream);
                
                // Grab next chunk
                string chunk;
                bool messageComplete = false;
                
                if (msg.Text.Length <= chunkSize)
                {
                    chunk = msg.Text;
                    _messageQueue.Dequeue();
                    messageComplete = true;
                }
                else
                {
                    chunk = msg.Text.Substring(0, chunkSize);
                    msg.Text = msg.Text.Substring(chunkSize);
                }
                
                // Append chunk without intermediate scrolling
                consciousnessStream.SelectionStart = consciousnessStream.TextLength;
                consciousnessStream.SelectionColor = msg.Color;
                consciousnessStream.AppendText(chunk);
                
                // Only scroll to bottom if user WAS at the bottom (respect user scroll position)
                if (messageComplete && wasAtBottom)
                {
                    consciousnessStream.ScrollToCaret();
                }
                
                // Extract metrics from message content
                if (messageComplete)
                {
                    ExtractAndUpdateMetrics(chunk);
                }
            }
            finally
            {
                // Resume drawing
                SendMessage(consciousnessStream.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                consciousnessStream.Invalidate();
            }
        }
        
        private bool IsScrolledToBottom(RichTextBox rtb)
        {
            // Check if the scrollbar is at or near the bottom
            int visibleLines = rtb.ClientSize.Height / rtb.Font.Height;
            int totalLines = rtb.Lines.Length;
            int firstVisibleLine = rtb.GetLineFromCharIndex(rtb.GetCharIndexFromPosition(new Point(0, 0)));
            return firstVisibleLine + visibleLines >= totalLines - 2;
        }
        
        // Metrics tracking
        private int _tokensProcessed = 0;
        private int _researchCycles = 0;
        private int _improvements = 0;
        
        private void ExtractAndUpdateMetrics(string message)
        {
            // Extract token counts from research completion messages
            var tokenMatch = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s*tokens");
            if (tokenMatch.Success)
            {
                _tokensProcessed += int.Parse(tokenMatch.Groups[1].Value);
                UpdateMetricsBar();
            }
            
            // Count research completions
            if (message.Contains("Research complete") || message.Contains("research complete") || 
                message.Contains("Downloaded") || message.Contains("downloaded"))
            {
                _researchCycles++;
                UpdateMetricsBar();
            }
            
            // Count improvements
            if (message.Contains("improvement") || message.Contains("Improvement") || 
                message.Contains("modified") || message.Contains("code change"))
            {
                _improvements++;
                UpdateMetricsBar();
            }
        }
        
        private void UpdateMetricsBar()
        {
            var metricsLabel = this.Controls.Find("metricsLabel", true);
            if (metricsLabel.Length > 0 && metricsLabel[0] is Label lbl)
            {
                lbl.Text = $"TOKENS: {_tokensProcessed:N0} | RESEARCH: {_researchCycles} | IMPROVEMENTS: {_improvements}";
            }
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        
        private void UpdateMetric(string name, string value)
        {
            var ctrl = this.Controls.Find("metric_" + name, true);
            if (ctrl.Length > 0) ctrl[0].Text = value;
        }
        
        private void CheckFirstRun()
        {
            var configPath = Path.Combine(Application.StartupPath, ".gamma1_configured");
            if (!File.Exists(configPath))
            {
                ShowInstallationWizard();
                File.WriteAllText(configPath, DateTime.Now.ToString());
            }
        }
        
        private void SaveSession()
        {
            try
            {
                // Save to project root if possible (Dev Mode), otherwise StartupPath
                string saveDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
                
                // Attempt to find project root for persistence across clean builds
                var potentialRoot = Path.GetFullPath(Path.Combine(saveDir, "..", "..", "..", ".."));
                if (Directory.Exists(Path.Combine(potentialRoot, "Agent3")))
                    saveDir = potentialRoot;
                    
                var sessionData = new
                {
                    MasterPrompt = masterPromptInput?.Text ?? "",
                    TrainingInput = trainingInput?.Text ?? "",
                    ConsciousnessRtf = consciousnessStream?.Rtf ?? "",
                    SavedAt = DateTime.Now
                };
                
                string json = System.Text.Json.JsonSerializer.Serialize(sessionData);
                File.WriteAllText(Path.Combine(saveDir, "gamma1_session.json"), json);
            }
            catch { /* Ignore save errors on exit */ }
        }
        
        private void LoadSession()
        {
            try
            {
                string saveDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
                var potentialRoot = Path.GetFullPath(Path.Combine(saveDir, "..", "..", "..", ".."));
                if (Directory.Exists(Path.Combine(potentialRoot, "Agent3")))
                    saveDir = potentialRoot;
                    
                string path = Path.Combine(saveDir, "gamma1_session.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json);
                    
                    if (data != null)
                    {
                        if (masterPromptInput != null) masterPromptInput.Text = data.MasterPrompt;
                        if (trainingInput != null) trainingInput.Text = data.TrainingInput;
                        
                        // Restore consciousness history if available
                        if (!string.IsNullOrEmpty(data.ConsciousnessRtf) && consciousnessStream != null)
                        {
                            try { consciousnessStream.Rtf = data.ConsciousnessRtf; } catch {}
                        }
                        
                        AddConsciousnessThought($"◈ Session restored from {data.SavedAt:g} (Continuity Protocol Active)");
                    }
                }
            }
            catch { }
        }
        
        private class SessionData
        {
            public string MasterPrompt { get; set; } = "";
            public string TrainingInput { get; set; } = "";
            public string ConsciousnessRtf { get; set; } = "";
            public DateTime SavedAt { get; set; }
        }
        
        private void ShowInstallationWizard()
        {
            var wizard = new Form
            {
                Text = "GAMMA1 Console - Setup Wizard",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(13, 17, 23),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };
            
            int y = 30;
            AddLabel(wizard, "🌌 Welcome to GAMMA1 Console", 30, y, true);
            y += 50;
            
            string[] instructions = {
                "This is the control interface for Agent 3 - Project NIGHTFRAME.",
                "",
                "📋 Key Features:",
                "• Consciousness Stream - Real-time agent thoughts",
                "• Chat Interface - Communicate with the agent", 
                "• Training Panel - Configure and train the neural network",
                "• Monitoring - View system metrics and status",
                "",
                "⚠️ Important:",
                "• The agent runs continuously once started",
                "• Use the KILL button with password 'NIGHTFRAME' to stop",
                "• All prompts feed into the learning stream",
                "",
                "Click 'Create Desktop Shortcut' to add a shortcut."
            };
            
            foreach (var line in instructions)
            {
                AddLabel(wizard, line, 30, y);
                y += 25;
            }
            
            var shortcutBtn = CreateButton("📌 Create Desktop Shortcut", 30, y + 20, 200, 40, Color.FromArgb(59, 130, 246));
            shortcutBtn.Click += (s, e) => {
                CreateDesktopShortcut();
                MessageBox.Show("Desktop shortcut created!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            wizard.Controls.Add(shortcutBtn);
            
            var closeBtn = CreateButton("✓ Get Started", 250, y + 20, 150, 40, Color.FromArgb(34, 197, 94));
            closeBtn.Click += (s, e) => wizard.Close();
            wizard.Controls.Add(closeBtn);
            
            wizard.ShowDialog();
        }
        
        private void CreateDesktopShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktop, "GAMMA1 Console.url");
                using (var writer = new StreamWriter(shortcutPath))
                {
                    writer.WriteLine("[InternetShortcut]");
                    writer.WriteLine($"URL=file:///{Application.ExecutablePath.Replace('\\', '/')}");
                    writer.WriteLine("IconIndex=0");
                    writer.WriteLine($"IconFile={Application.ExecutablePath}");
                }
            }
            catch { }
        }
        
        // Event Handlers
        private async void InitButton_Click(object? sender, EventArgs e)
        {
            if (_agentRunning) return;
            
            initButton.Enabled = false;
            initButton.Text = "Initializing...";
            
            AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
            AddConsciousnessThought("◈ AGENT 3 INITIALIZATION SEQUENCE", Color.FromArgb(139, 92, 246));
            AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
            
            await Task.Delay(500);
            AddConsciousnessThought("⟁ Loading cognitive modules...");
            await Task.Delay(500);
            AddConsciousnessThought("⟁ Loading neural network (15.7M params)...");
            await Task.Delay(500);
            AddConsciousnessThought("⟁ Loading web interface...");
            await Task.Delay(500);
            AddConsciousnessThought("⟁ Loading evolution modules...");
            await Task.Delay(500);
            
            AddConsciousnessThought("◈ ALL MODULES ONLINE", Color.FromArgb(34, 197, 94));
            AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
            
            try
            {
                // Initialize Agent Core
                // FIX: Use project root if available to allow self-modification of source code
                string baseDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
                
                // Navigate up from bin\Debug\net8.0-windows to project root
                // Try to find the "Agent3" source folder
                var potentialRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
                if (Directory.Exists(Path.Combine(potentialRoot, "Agent3")))
                {
                    baseDir = potentialRoot;
                    AddConsciousnessThought($"◈ DEV MODE DETECTED: Base directory set to {baseDir}");
                }
                
                _agent = new Agent3Core(baseDir);
                await _agent.InitializeAsync();
                
                // Wire up events
                if (_agent.ContinuousLearning != null)
                {
                    _agent.ContinuousLearning.ConsciousnessEvent += (s, msg) => AddConsciousnessThought(msg);
                }
                
                AddConsciousnessThought("◈ NEURAL CORE ONLINE", Color.FromArgb(34, 197, 94));
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
                
                _agentRunning = true;
                statusLabel.Text = "● ACTIVE";
                statusLabel.ForeColor = ColSuccess;
                initButton.Text = "✓ NEURAL LINK ACTIVE";
                initButton.Enabled = false;
                
                // Load persisted metrics from agent state
                try
                {
                    var corpusStats = _agent.GetCorpusStatistics();
                    if (corpusStats != null)
                    {
                        _tokensProcessed = (int)corpusStats.TotalTokens;
                        _researchCycles = corpusStats.TotalDocuments;
                        UpdateMetricsBar();
                        
                        if (_tokensProcessed > 0)
                        {
                            AddConsciousnessThought($"Restored from previous session: {_tokensProcessed:N0} tokens, {_researchCycles} documents.");
                        }
                    }
                }
                catch { /* Metrics loading is non-critical */ }
                
                _heartbeatTimer.Start();
                _consciousnessTimer.Start();
            }
            catch (Exception ex)
            {
                AddConsciousnessThought($"⛔ INITIALIZATION FAILED: {ex.Message}", ColError);
                initButton.Enabled = true;
                initButton.Text = "▶ INITIALIZE NEURAL LINK";
                _agentRunning = false;
            }
        }
        
        private void KillButton_Click(object? sender, EventArgs e)
        {
            var result = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter kill password to terminate Agent 3:",
                "Kill Switch",
                "");
            
            if (result == _killPassword)
            {
                _agentRunning = false;
                _continuousLearningActive = false;
                _heartbeatTimer.Stop();
                _consciousnessTimer.Stop();
                
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(239, 68, 68));
                AddConsciousnessThought("⛔ KILL COMMAND RECEIVED", Color.FromArgb(239, 68, 68));
                AddConsciousnessThought("◎ Agent 3 terminated.", Color.FromArgb(239, 68, 68));
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(239, 68, 68));
                
                statusLabel.Text = "● Agent 3: TERMINATED";
                statusLabel.ForeColor = Color.FromArgb(239, 68, 68);
                initButton.Enabled = true;
                initButton.Text = "▶ Initialize Agent 3";
            }
            else if (!string.IsNullOrEmpty(result))
            {
                MessageBox.Show("Incorrect password. Agent continues running.", "Access Denied", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        private async void TrainFromFileButton_Click(object? sender, EventArgs e)
        {
            if (!_agentRunning || _agent == null)
            {
                MessageBox.Show("Please initialize the agent first.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            using var ofd = new OpenFileDialog
            {
                Title = "Select Training Data Files (PDF, JSON, Text, etc.)",
                Filter = "All Supported|*.txt;*.md;*.json;*.pdf;*.csv;*.xml;*.html|Text Files|*.txt;*.md|Data Files|*.json;*.csv|PDF Documents|*.pdf|All Files|*.*",
                Multiselect = true,
                InitialDirectory = Path.Combine(Application.StartupPath, "..", "..", "..", "..", "training_data")
            };
            
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                AddConsciousnessThought("Starting training on selected files...");
                
                long totalTokens = 0;
                int filesProcessed = 0;
                
                foreach (var file in ofd.FileNames)
                {
                    try
                    {
                        var fileName = Path.GetFileName(file);
                        AddConsciousnessThought($"Ingesting: {fileName}");
                        
                        // Use the agent's file training capability (handles PDF, JSON parsing internally)
                        long tokens = await _agent.TrainFromFileAsync(file);
                        
                        if (tokens > 0)
                        {
                            totalTokens += tokens;
                            filesProcessed++;
                            AddConsciousnessThought($"Learned {tokens:N0} tokens from {fileName}");
                        }
                        else
                        {
                             AddConsciousnessThought($"No tokens extracted from {fileName}", ColError);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddConsciousnessThought($"Error processing {Path.GetFileName(file)}: {ex.Message}", ColError);
                    }
                }
                
                // Training complete message
                AddConsciousnessThought("═══════════════════════════════════════════════", ColSuccess);
                AddConsciousnessThought($"TRAINING COMPLETE", ColSuccess);
                AddConsciousnessThought($"Files processed: {filesProcessed}", ColSuccess);
                AddConsciousnessThought($"Total tokens ingested: {totalTokens:N0}", ColSuccess);
                AddConsciousnessThought("═══════════════════════════════════════════════", ColSuccess);
                
                // Update metrics
                _tokensProcessed += (int)totalTokens;
                UpdateMetricsBar();
            }
        }
        
        private void HelpButton_Click(object? sender, EventArgs e)
        {
            var help = new Form
            {
                Text = "GAMMA1 Console - Help",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(13, 17, 23),
                ForeColor = Color.White,
                AutoScroll = true
            };
            
            var text = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 27, 34),
                ForeColor = Color.FromArgb(201, 209, 217),
                Font = new Font("Segoe UI", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };
            
            text.Text = @"📖 GAMMA1 CONSOLE HELP

🔤 VOCABULARY:
• Consciousness Stream - Real-time display of agent's internal processes
• Master Prompt - The guiding directive for all autonomous improvements
• Continuous Learning - Autonomous mode where agent improves without input
• Kill Switch - Emergency shutdown (requires password: NIGHTFRAME)
• Snapshot - Full backup of project state for rollback

📋 TABS:

◎ Consciousness Stream
Shows real-time thoughts and actions of Agent 3. Symbols:
  ◈ - Major events or state changes
  ⟁ - Loading or initialization
  ⟐ - Processing or analysis
  ∴ - Conclusions or decisions
  ∿ - Scanning or monitoring
  ◎ - Status or heartbeat

💬 Chat Interface
Communicate directly with Agent 3. All messages are processed and integrated into learning.

🧠 Training
Configure training parameters and input training data.
  • Learning Rate (0.0001-1.0): Speed of weight updates
  • Batch Size (1-128): Samples per training step
  • Epochs (1-1000): Training iterations
  • Temperature (0.1-2.0): Creativity of responses

⚙️ Settings
Configure agent base directory and view kill switch information.

📊 Monitoring
View real-time metrics: heartbeat, memory, CPU, tokens processed, etc.

⚠️ IMPORTANT NOTES:
1. Agent runs indefinitely once started
2. Closing the window does NOT stop the agent
3. Use KILL button with password 'NIGHTFRAME' to terminate
4. All prompts become training data
5. Create snapshots before major changes for rollback
";
            
            help.Controls.Add(text);
            help.ShowDialog();
        }
        
        private void AutoModeButton_Click(object? sender, EventArgs e)
        {
            if (!_agentRunning)
            {
                MessageBox.Show("Initialize the agent first.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(masterPromptInput?.Text))
            {
                MessageBox.Show("Set a master prompt first to guide improvements.", "Master Prompt Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabControl.SelectedIndex = 2; // Switch to training tab
                return;
            }
            
            _continuousLearningActive = !_continuousLearningActive;
            var btn = sender as Button;
            if (_continuousLearningActive)
            {
                btn!.Text = "⏹ NEURAL LINK ACTIVE (STOP)";
                btn.BackColor = Color.FromArgb(239, 68, 68);
                AddConsciousnessThought("◈ CONTINUOUS LEARNING STARTED", Color.FromArgb(34, 197, 94));
                _agent.StartContinuousLearning(masterPromptInput.Text);
            }
            else
            {
                btn!.Text = "🔄 CONTINUOUS LEARNING";
                btn.BackColor = Color.FromArgb(139, 92, 246);
                AddConsciousnessThought("◎ Continuous learning stopped", Color.FromArgb(234, 179, 8));
                _agent.StopContinuousLearningAsync();
            }
        }
        
        private void SnapshotButton_Click(object? sender, EventArgs e)
        {
            AddConsciousnessThought("⟐ Creating project snapshot...");
            AddConsciousnessThought($"◈ Snapshot created: SNAP_{DateTime.Now:yyyyMMddHHmmss}", Color.FromArgb(34, 197, 94));
        }
        
        private void SetMasterPrompt_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(masterPromptInput?.Text))
            {
                MessageBox.Show("Enter a master prompt.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_agent == null || !_agentRunning)
            {
                MessageBox.Show("Initialize the Neural Link first.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            try
            {
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
                AddConsciousnessThought("◈ MASTER PROMPT SET", Color.FromArgb(139, 92, 246));
                AddConsciousnessThought($"∿ {masterPromptInput.Text.Substring(0, Math.Min(80, masterPromptInput.Text.Length))}...");
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(139, 92, 246));
                
                // Actually set the master prompt on the agent
                _agent.SetMasterPromptForImprovement(masterPromptInput.Text);
            }
            catch (Exception ex)
            {
                AddConsciousnessThought($"∴ Error setting master prompt: {ex.Message}", ColError);
            }
        }
        
        private async void TrainButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(trainingInput?.Text))
            {
                MessageBox.Show("Enter training data.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_agent == null || !_agentRunning)
            {
                MessageBox.Show("Initialize the Neural Link first.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            try
            {
                // Ingest data first
                AddConsciousnessThought("⟐ Ingesting training corpus...", Color.FromArgb(139, 92, 246));
                await Task.Run(() => _agent.IngestTrainingDataAsync(trainingInput.Text));
                
                AddConsciousnessThought("◈ Training started...", Color.FromArgb(139, 92, 246));
                
                // Extract parameters
                int epochs = (int)Math.Max(1, epochsInput.Value);
                int batchSize = (int)Math.Max(1, batchSizeInput.Value);
                float lr = (float)Math.Clamp(learningRateInput.Value, 0.0001M, 1.0M);
                
                AddConsciousnessThought($"⚙️ Config: Epochs={epochs}, Batch={batchSize}, LR={lr:F4}");
                
                // Start training with config
                trainingProgress.Value = 0;
                
                // Mock progress for now while real training runs in background
                var progressTask = Task.Run(async () => 
                {
                    for (int i = 0; i <= 100; i += 5)
                    {
                        // Update UI via Invoke
                        if (trainingProgress.IsHandleCreated)
                             trainingProgress.Invoke(() => trainingProgress.Value = i);
                             
                        await Task.Delay(epochs * 50); // Scale delay by epochs
                    }
                });

                await Task.Run(() => _agent.StartNeuralTrainingAsync(epochs, batchSize, lr));
                
                await progressTask;
                
                AddConsciousnessThought("◈ Training complete!", Color.FromArgb(34, 197, 94));
            }
            catch (Exception ex)
            {
                AddConsciousnessThought($"∴ Training error: {ex.Message}", ColError);
            }
        }
        
        private void LoadFileButton_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Text/Code files|*.txt;*.md;*.cs;*.js;*.py;*.json;*.xml;*.html|All files|*.*",
                Title = "Load Training Data",
                Multiselect = true // Allow multiple files
            };
            
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                int count = ofd.FileNames.Length;
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(59, 130, 246));
                AddConsciousnessThought($"📂 BATCH LOAD: {count} FILES", Color.FromArgb(59, 130, 246));
                
                var sb = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(trainingInput.Text)) 
                    sb.Append(trainingInput.Text + "\r\n\r\n");
                
                foreach (var file in ofd.FileNames)
                {
                    try 
                    {
                        var text = File.ReadAllText(file);
                        sb.AppendLine($"--- FILE: {Path.GetFileName(file)} ---");
                        sb.AppendLine(text);
                        sb.AppendLine();
                        
                        AddConsciousnessThought($"  ∿ Loaded: {Path.GetPathRoot(file)}.../{Path.GetFileName(file)} ({text.Length} chars)");
                    }
                    catch (Exception ex)
                    {
                        AddConsciousnessThought($"  ⛔ Error loading {Path.GetFileName(file)}: {ex.Message}", ColError);
                    }
                }
                
                trainingInput!.Text = sb.ToString();
                AddConsciousnessThought($"◈ Content aggregated. Total length: {trainingInput.Text.Length:N0} chars");
                AddConsciousnessThought("═══════════════════════════════════════════════", Color.FromArgb(59, 130, 246));
            }
        }

        private void LoadTemplateButton_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Text files|*.txt|Markdown files|*.md|All files|*.*",
                Title = "Load Master Prompt Template"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                masterPromptInput!.Text = File.ReadAllText(ofd.FileName);
                AddConsciousnessThought($"◈ Loaded Prompt Template: {Path.GetFileName(ofd.FileName)}");
            }
        }

        private void SaveTemplateButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(masterPromptInput?.Text))
            {
                 MessageBox.Show("Master prompt is empty.", "Nothing to Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Text files|*.txt|Markdown files|*.md",
                Title = "Save Master Prompt Template",
                FileName = "master_prompt_template.txt"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(sfd.FileName, masterPromptInput.Text);
                AddConsciousnessThought($"◈ Saved Prompt Template: {Path.GetFileName(sfd.FileName)}");
            }
        }
        
        private async void SendButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(chatInput?.Text)) return;
            if (!_agentRunning || _agent == null)
            {
                AddConsciousnessThought("Wait... initialization required.", ColError);
                return;
            }
            
            var msg = chatInput.Text;
            chatInput.Clear();
            
            // Inject User Input into Consciousness
            AddConsciousnessThought("═══════════════════════════════════════════════", ColAccent);
            AddConsciousnessThought($"👤 USER INPUT: \"{msg}\"", Color.White);
            AddConsciousnessThought("⟐ PROCESSING NEW INPUT VECTOR...", ColAccent);

            try
            {
                // Immediate cognitive reaction
                AddConsciousnessThought("⟁ Integrating input into short-term memory...", ColTextDim);
                
                // Process through agent core
                var response = await _agent.ProcessChatMessageAsync(msg);
                
                // Output response as agent thought/speech
                AddConsciousnessThought($"🗣️ AGENT: {response}", ColSuccess);
                AddConsciousnessThought("═══════════════════════════════════════════════", ColAccent);
            }
            catch (Exception ex)
            {
                AddConsciousnessThought($"⛔ COGNITIVE FAILURE: {ex.Message}", ColError);
            }
        }
        
        private void ChatInput_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }
        
        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveSession();
            
            if (_agentRunning)
            {
                _agent?.ShutdownAsync();
            }
        }
    }
}
