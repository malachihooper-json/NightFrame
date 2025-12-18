/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║               ENHANCED PORTAL PAGES - CROSS-PLATFORM UX                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Beautiful, user-friendly portal pages with clear explanations.            ║
 * ║  Uses local assets - no external hosting dependencies.                     ║
 * ║  Explains how the mesh network works and benefits to users.                ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Enhanced portal pages with improved UX and local asset serving.
/// </summary>
public static class PortalPages
{
    // Common CSS used across all platforms
    private static readonly string CommonStyles = """
        * { margin: 0; padding: 0; box-sizing: border-box; }
        
        @keyframes float { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }
        @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }
        @keyframes glow { 0%, 100% { box-shadow: 0 0 20px rgba(124, 58, 237, 0.3); } 50% { box-shadow: 0 0 40px rgba(124, 58, 237, 0.6); } }
        
        :root {
            --primary: #7c3aed;
            --secondary: #06b6d4;
            --bg-dark: #0a0a0f;
            --bg-card: rgba(30, 30, 45, 0.9);
            --text-primary: #f8fafc;
            --text-secondary: #94a3b8;
            --text-muted: #6b7280;
            --border: rgba(124, 58, 237, 0.3);
            --success: #22c55e;
        }
        
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, var(--bg-dark) 0%, #1a1a2e 50%, #0f0f1a 100%);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            color: var(--text-primary);
            padding: 20px;
            line-height: 1.6;
        }
        
        .container {
            max-width: 560px;
            width: 100%;
            animation: fadeIn 0.5s ease-out;
        }
        
        .logo-section {
            text-align: center;
            margin-bottom: 28px;
        }
        
        .logo {
            width: 120px;
            height: 120px;
            animation: float 4s ease-in-out infinite;
            margin-bottom: 16px;
            /* Black logo on transparent - add white glow for visibility on dark bg */
            filter: drop-shadow(0 0 10px rgba(255,255,255,0.8)) drop-shadow(0 0 20px rgba(124,58,237,0.5));
        }
        
        /* Mobile responsive adjustments */
        @media (max-width: 480px) {
            body { padding: 12px; }
            .container { max-width: 100%; }
            .logo { width: 80px; height: 80px; }
            h1 { font-size: 22px; }
            .subtitle { font-size: 14px; }
            .card { padding: 18px; border-radius: 16px; }
            .benefits { grid-template-columns: 1fr; gap: 10px; }
            .btn-primary { padding: 16px 20px; font-size: 16px; }
            .step { gap: 12px; }
            .step-number { width: 24px; height: 24px; font-size: 12px; }
        }
        
        /* Tablet */
        @media (min-width: 481px) and (max-width: 768px) {
            .container { max-width: 90%; }
            .logo { width: 100px; height: 100px; }
        }
        
        h1 {
            font-size: 28px;
            font-weight: 700;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 8px;
        }
        
        .subtitle {
            color: var(--text-secondary);
            font-size: 16px;
        }
        
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 20px;
            padding: 28px;
            margin-bottom: 20px;
            backdrop-filter: blur(10px);
        }
        
        .how-it-works {
            background: linear-gradient(135deg, rgba(124, 58, 237, 0.1), rgba(6, 182, 212, 0.1));
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 20px;
            margin-bottom: 24px;
        }
        
        .how-it-works h2 {
            font-size: 16px;
            margin-bottom: 12px;
            color: var(--text-primary);
        }
        
        .how-it-works p {
            font-size: 14px;
            color: var(--text-secondary);
            margin-bottom: 12px;
        }
        
        .how-it-works ul {
            list-style: none;
            padding: 0;
        }
        
        .how-it-works li {
            font-size: 14px;
            color: var(--text-secondary);
            padding: 8px 0;
            padding-left: 28px;
            position: relative;
        }
        
        .how-it-works li::before {
            content: "✓";
            position: absolute;
            left: 0;
            color: var(--success);
            font-weight: bold;
        }
        
        .benefits {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 12px;
            margin-bottom: 24px;
        }
        
        .benefit {
            background: rgba(124, 58, 237, 0.1);
            border: 1px solid rgba(124, 58, 237, 0.2);
            border-radius: 12px;
            padding: 16px;
            text-align: center;
        }
        
        .benefit-icon { font-size: 24px; margin-bottom: 8px; }
        .benefit-title { font-weight: 600; font-size: 13px; color: var(--text-primary); }
        .benefit-desc { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
        
        /* Trust badges - Product Research recommendation */
        .trust-badges {
            display: flex;
            justify-content: center;
            gap: 10px;
            flex-wrap: wrap;
            margin: 16px 0;
        }
        .trust-badge {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            padding: 6px 12px;
            background: rgba(34, 197, 94, 0.1);
            border: 1px solid rgba(34, 197, 94, 0.3);
            border-radius: 20px;
            font-size: 12px;
            color: var(--success);
        }
        
        /* FAQ section - expandable */
        .faq { margin-top: 20px; }
        .faq-item {
            background: rgba(255,255,255,0.03);
            border: 1px solid rgba(255,255,255,0.08);
            border-radius: 10px;
            margin-bottom: 8px;
            overflow: hidden;
        }
        .faq-question {
            padding: 14px 16px;
            cursor: pointer;
            display: flex;
            justify-content: space-between;
            align-items: center;
            font-weight: 500;
            color: var(--text-primary);
        }
        .faq-question::after { content: '+'; font-size: 18px; }
        .faq-item.open .faq-question::after { content: '−'; }
        .faq-answer {
            max-height: 0;
            overflow: hidden;
            transition: max-height 0.3s;
            padding: 0 16px;
            color: var(--text-secondary);
            font-size: 13px;
        }
        .faq-item.open .faq-answer {
            max-height: 200px;
            padding: 0 16px 14px;
        }
        
        /* Network stats */
        .network-stats {
            display: flex;
            justify-content: space-around;
            padding: 16px;
            background: rgba(124, 58, 237, 0.05);
            border-radius: 12px;
            margin-bottom: 16px;
        }
        .stat { text-align: center; }
        .stat-value { font-size: 24px; font-weight: 700; color: var(--primary); }
        .stat-label { font-size: 11px; color: var(--text-muted); }
        
        .btn-primary {
            display: block;
            width: 100%;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            color: white;
            text-decoration: none;
            padding: 18px 28px;
            border-radius: 14px;
            font-weight: 700;
            font-size: 17px;
            text-align: center;
            transition: all 0.3s;
            box-shadow: 0 4px 20px rgba(124, 58, 237, 0.4);
            cursor: pointer;
            border: none;
            animation: glow 3s ease-in-out infinite;
        }
        
        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 30px rgba(124, 58, 237, 0.6);
        }
        
        .btn-secondary {
            display: block;
            width: 100%;
            background: transparent;
            border: 1px solid #374151;
            color: var(--text-secondary);
            text-decoration: none;
            padding: 14px 24px;
            border-radius: 12px;
            font-size: 14px;
            text-align: center;
            transition: all 0.3s;
        }
        
        .btn-secondary:hover {
            border-color: var(--primary);
            color: var(--text-primary);
        }
        
        .steps {
            margin-top: 20px;
        }
        
        .step {
            display: flex;
            align-items: flex-start;
            gap: 14px;
            padding: 14px 0;
            border-bottom: 1px solid rgba(255,255,255,0.08);
        }
        
        .step:last-child { border-bottom: none; }
        
        .step-number {
            width: 28px;
            height: 28px;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 700;
            font-size: 13px;
            flex-shrink: 0;
        }
        
        .step-content h3 { font-size: 14px; font-weight: 600; margin-bottom: 4px; }
        .step-content p { font-size: 13px; color: var(--text-secondary); }
        
        .note {
            text-align: center;
            font-size: 12px;
            color: var(--text-muted);
            margin-top: 16px;
            line-height: 1.6;
        }
        
        .platform-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: rgba(100, 100, 100, 0.2);
            border: 1px solid rgba(150, 150, 150, 0.3);
            padding: 8px 14px;
            border-radius: 8px;
            font-size: 12px;
            color: #a1a1aa;
            margin-bottom: 20px;
        }
        
        .comparison {
            display: flex;
            gap: 12px;
            margin: 20px 0;
        }
        
        .comparison-item {
            flex: 1;
            background: rgba(0,0,0,0.3);
            border-radius: 12px;
            padding: 16px;
            text-align: center;
        }
        
        .comparison-label { font-size: 11px; color: var(--text-muted); margin-bottom: 4px; }
        .comparison-value { font-size: 18px; font-weight: 700; }
        .comparison-value.limited { color: #f59e0b; }
        .comparison-value.unlimited { color: var(--success); }
        
        code {
            background: rgba(124, 58, 237, 0.2);
            padding: 2px 6px;
            border-radius: 4px;
            font-family: 'SF Mono', Monaco, monospace;
            font-size: 12px;
        }
        
        .terms-link {
            color: var(--text-muted);
            text-decoration: underline;
        }
    """;

    // ═══════════════════════════════════════════════════════════════════════════
    //                          WINDOWS PORTAL PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetWindowsPortalHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network - Free Internet</title>
    <style>{{CommonStyles}}</style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\'font-size:64px;animation:float 4s ease-in-out infinite;\'>◈</div>'">
            <h1>Welcome to NFRAME Network</h1>
            <p class="subtitle">Free Unlimited Internet, Powered by People Like You</p>
        </div>
        
        <div class="card">
            <div class="how-it-works">
                <h2>🤔 How Does This Work?</h2>
                <p>NFRAME is a <strong>community-powered internet network</strong>. People share small amounts of their bandwidth and computing power, and in return, everyone gets free internet access anywhere in the network.</p>
                <p>Think of it like carpooling, but for internet. When you join:</p>
                <ul>
                    <li>You get <strong>unlimited high-speed internet</strong> at any NFRAME hotspot</li>
                    <li>You share a small portion of your connection when you're online</li>
                    <li>You earn credits to use AI and other network services</li>
                    <li>You help build a global free internet network</li>
                </ul>
            </div>
            
            <div class="comparison">
                <div class="comparison-item">
                    <div class="comparison-label">Without App</div>
                    <div class="comparison-value limited">512 Kbps</div>
                </div>
                <div class="comparison-item">
                    <div class="comparison-label">With App</div>
                    <div class="comparison-value unlimited">∞ Unlimited</div>
                </div>
            </div>
            
            <a href="/download/brain.exe" class="btn-primary" id="downloadBtn">
                ⬇️ Join Network (Download for Windows)
            </a>
            
            <div class="steps">
                <div class="step">
                    <div class="step-number">1</div>
                    <div class="step-content">
                        <h3>Download & Run</h3>
                        <p>Click download, open <code>brain.exe</code>. If Windows warns you, click "More info" → "Run anyway"</p>
                    </div>
                </div>
                <div class="step">
                    <div class="step-number">2</div>
                    <div class="step-content">
                        <h3>Allow Network Access</h3>
                        <p>When Windows Firewall asks, click "Allow access" so you can connect to the network</p>
                    </div>
                </div>
                <div class="step">
                    <div class="step-number">3</div>
                    <div class="step-content">
                        <h3>You're Part of the Network!</h3>
                        <p>NFRAME runs quietly in the background. Enjoy unlimited internet anywhere you see an NFRAME hotspot</p>
                    </div>
                </div>
            </div>
        </div>
        
        <a href="/success?guest=true" class="btn-secondary">Continue as Guest (Limited Speed)</a>
        
        <p class="note">
            By joining, you agree to share a small portion of bandwidth and compute.<br>
            NFRAME uses minimal resources (~2% CPU/RAM) and won't affect your experience.<br>
            <a href="/terms" class="terms-link">Read our Terms of Service</a>
        </p>
    </div>
    
    <script>
        document.getElementById('downloadBtn').addEventListener('click', function() {
            this.innerHTML = '✓ Downloading... Check your Downloads folder';
            this.style.background = 'linear-gradient(135deg, #22c55e, #06b6d4)';
            this.style.animation = 'none';
        });
    </script>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          macOS PORTAL PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetMacOSPortalHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network - Free Internet</title>
    <style>
        {{CommonStyles}}
        .terminal-box {
            background: #1a1a1a;
            border-radius: 8px;
            padding: 12px 16px;
            font-family: 'SF Mono', Monaco, monospace;
            font-size: 12px;
            margin-top: 8px;
            border: 1px solid #333;
        }
        .terminal-prompt { color: var(--success); }
        .terminal-cmd { color: var(--text-primary); }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\'font-size:64px;animation:float 4s ease-in-out infinite;\'>◈</div>'">
            <h1>Welcome to NFRAME Network</h1>
            <p class="subtitle">Free Unlimited Internet, Powered by People Like You</p>
        </div>
        
        <div class="card">
            <div class="how-it-works">
                <h2>🤔 How Does This Work?</h2>
                <p>NFRAME is a <strong>community-powered internet network</strong>. People share small amounts of their bandwidth and computing power, and in return, everyone gets free internet access anywhere in the network.</p>
                <ul>
                    <li>Get <strong>unlimited internet</strong> at any NFRAME hotspot worldwide</li>
                    <li>Your Mac's GPU helps process AI tasks for credits</li>
                    <li>Runs natively on Apple Silicon and Intel Macs</li>
                    <li>Uses under 2% of your resources when active</li>
                </ul>
            </div>
            
            <div class="comparison">
                <div class="comparison-item">
                    <div class="comparison-label">Without App</div>
                    <div class="comparison-value limited">512 Kbps</div>
                </div>
                <div class="comparison-item">
                    <div class="comparison-label">With App</div>
                    <div class="comparison-value unlimited">∞ Unlimited</div>
                </div>
            </div>
            
            <a href="/download/brain-osx" class="btn-primary" id="downloadBtn">
                ⬇️ Join Network (Download for Mac)
            </a>
            
            <div class="steps">
                <div class="step">
                    <div class="step-number">1</div>
                    <div class="step-content">
                        <h3>Download the App</h3>
                        <p>Click above. The file will appear in your Downloads folder</p>
                    </div>
                </div>
                <div class="step">
                    <div class="step-number">2</div>
                    <div class="step-content">
                        <h3>Open Terminal & Run</h3>
                        <p>Open Terminal (Cmd+Space, type "Terminal"), then paste:</p>
                        <div class="terminal-box">
                            <span class="terminal-prompt">$</span> <span class="terminal-cmd">chmod +x ~/Downloads/brain-osx && ~/Downloads/brain-osx</span>
                        </div>
                    </div>
                </div>
                <div class="step">
                    <div class="step-number">3</div>
                    <div class="step-content">
                        <h3>Allow in Security (if needed)</h3>
                        <p>If macOS blocks it: System Settings → Privacy & Security → "Allow Anyway"</p>
                    </div>
                </div>
            </div>
        </div>
        
        <a href="/success?guest=true" class="btn-secondary">Continue as Guest (Limited Speed)</a>
        
        <p class="note">
            By joining, you agree to share a small portion of bandwidth and compute.<br>
            Optimized for Apple Silicon (M1/M2/M3) and Intel Macs.<br>
            <a href="/terms" class="terms-link">Read our Terms of Service</a>
        </p>
    </div>
    
    <script>
        document.getElementById('downloadBtn').addEventListener('click', function() {
            this.innerHTML = '✓ Downloading...';
            this.style.background = 'linear-gradient(135deg, #22c55e, #06b6d4)';
            this.style.animation = 'none';
        });
    </script>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          ANDROID PORTAL PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetAndroidPortalHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
    <title>NFRAME Network - Free Internet</title>
    <style>
        {{CommonStyles}}
        body { padding: 0; }
        .top-section {
            flex: 1;
            padding: 32px 20px;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
        }
        .logo { width: 100px; height: 100px; }
        h1 { font-size: 24px; text-align: center; }
        .subtitle { margin-bottom: 24px; text-align: center; }
        .bottom-sheet {
            background: var(--bg-card);
            border-top: 1px solid var(--border);
            border-radius: 24px 24px 0 0;
            padding: 24px 20px;
            animation: slideUp 0.4s ease-out;
        }
        @keyframes slideUp { from { transform: translateY(100%); } to { transform: translateY(0); } }
        .handle {
            width: 40px;
            height: 4px;
            background: #374151;
            border-radius: 2px;
            margin: 0 auto 16px;
        }
        .quick-features {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
        }
        .quick-feature {
            flex: 1;
            background: rgba(124, 58, 237, 0.1);
            border-radius: 12px;
            padding: 12px 8px;
            text-align: center;
        }
        .quick-feature-icon { font-size: 20px; }
        .quick-feature-text { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
    </style>
</head>
<body>
    <div class="top-section">
        <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\'font-size:56px;animation:float 4s ease-in-out infinite;\'>◈</div>'">
        <h1>Join NFRAME Network</h1>
        <p class="subtitle">Free internet powered by the community</p>
        
        <div class="how-it-works" style="margin: 0; max-width: 100%;">
            <h2>🤔 How It Works</h2>
            <p>NFRAME is like internet carpooling. Members share a bit of their connection, and everyone gets free internet at any NFRAME hotspot.</p>
            <p><strong>Install the Scout app to:</strong></p>
            <ul>
                <li>Unlock unlimited speed (instead of 512 Kbps)</li>
                <li>Help map cell towers as you travel (no data cost)</li>
                <li>Earn credits for AI and network services</li>
            </ul>
        </div>
    </div>
    
    <div class="bottom-sheet">
        <div class="handle"></div>
        
        <div class="quick-features">
            <div class="quick-feature">
                <div class="quick-feature-icon">📶</div>
                <div class="quick-feature-text">Unlimited Speed</div>
            </div>
            <div class="quick-feature">
                <div class="quick-feature-icon">🔋</div>
                <div class="quick-feature-text">Low Battery</div>
            </div>
            <div class="quick-feature">
                <div class="quick-feature-icon">🌍</div>
                <div class="quick-feature-text">Works Globally</div>
            </div>
        </div>
        
        <a href="/download/scout.apk" class="btn-primary" id="downloadBtn">
            ⬇️ Install Scout App (APK)
        </a>
        
        <div class="steps" style="margin: 16px 0;">
            <div class="step">
                <div class="step-number">1</div>
                <div class="step-content">
                    <h3>Download APK</h3>
                    <p>Tap above, then tap the downloaded file</p>
                </div>
            </div>
            <div class="step">
                <div class="step-number">2</div>
                <div class="step-content">
                    <h3>Allow Install</h3>
                    <p>If asked, enable "Install from this source" in Settings</p>
                </div>
            </div>
        </div>
        
        <a href="/success?guest=true" class="btn-secondary">Browse as Guest (512 Kbps)</a>
        
        <p class="note" style="font-size: 11px;">
            Scout uses minimal data and battery. Your location helps map the network.<br>
            <a href="/terms" class="terms-link">Terms of Service</a>
        </p>
    </div>
    
    <script>
        document.getElementById('downloadBtn').addEventListener('click', function() {
            this.innerHTML = '✓ Downloading APK...';
            this.style.background = 'linear-gradient(135deg, #22c55e, #06b6d4)';
            this.style.animation = 'none';
        });
    </script>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          iOS PORTAL PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetIOSPortalHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
    <meta name="apple-mobile-web-app-capable" content="yes">
    <title>NFRAME Network - Free Internet</title>
    <style>
        {{CommonStyles}}
        body { padding: 0; -webkit-text-size-adjust: 100%; }
        .container {
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            padding: 20px;
        }
        .content {
            flex: 1;
            display: flex;
            flex-direction: column;
            justify-content: center;
        }
        .logo { width: 100px; height: 100px; }
        h1 { font-size: 26px; text-align: center; }
        .success-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: rgba(34, 197, 94, 0.15);
            border: 1px solid rgba(34, 197, 94, 0.3);
            padding: 12px 20px;
            border-radius: 12px;
            color: var(--success);
            font-weight: 600;
            margin: 20px 0;
        }
        .info-card {
            background: rgba(124, 58, 237, 0.1);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 20px;
            margin: 16px 0;
        }
        .info-card h3 { font-size: 15px; margin-bottom: 10px; }
        .info-card p { font-size: 14px; color: var(--text-secondary); }
    </style>
</head>
<body>
    <div class="container">
        <div class="content">
            <div class="logo-section">
                <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\'font-size:56px;animation:float 4s ease-in-out infinite;\'>◈</div>'">
                <h1>You're Connected!</h1>
                <p class="subtitle">Welcome to NFRAME Global Network</p>
            </div>
            
            <div style="text-align: center;">
                <div class="success-badge">
                    <span>✓</span> Internet access enabled
                </div>
            </div>
            
            <div class="info-card">
                <h3>🤔 What is NFRAME?</h3>
                <p>NFRAME is a community-powered internet network. People around the world share small amounts of their internet, creating free WiFi hotspots everywhere.</p>
                <p style="margin-top: 12px;"><strong>You're connected as a guest right now.</strong> Browse freely at 512 Kbps - fast enough for most browsing and messaging.</p>
            </div>
            
            <div class="info-card">
                <h3>📱 Want Unlimited Speed?</h3>
                <p>Install Scout on a computer (Windows, Mac, or Linux) to unlock unlimited speed at any NFRAME hotspot. Scout contributors get priority access everywhere.</p>
            </div>
            
            <div class="info-card">
                <h3>🔒 Your Privacy</h3>
                <p>We don't track your browsing. We don't sell your data. The network is encrypted end-to-end. <a href="/terms" class="terms-link">Read our privacy policy</a>.</p>
            </div>
        </div>
        
        <a href="/" class="btn-secondary" style="margin-bottom: 20px;">Close Portal</a>
        
        <p class="note">
            You can close this page and browse normally.<br>
            Look for NFRAME hotspots anywhere you go!
        </p>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          LINUX PORTAL PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetLinuxPortalHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network - Free Internet</title>
    <style>
        {{CommonStyles}}
        .terminal-box {
            background: #1a1a1a;
            border-radius: 8px;
            padding: 12px 16px;
            font-family: 'Ubuntu Mono', 'DejaVu Sans Mono', monospace;
            font-size: 13px;
            margin-top: 8px;
            border: 1px solid #333;
            overflow-x: auto;
        }
        .terminal-prompt { color: var(--success); }
        .terminal-cmd { color: var(--text-primary); }
        .distro-select {
            display: flex;
            gap: 8px;
            margin-bottom: 16px;
        }
        .distro-btn {
            flex: 1;
            background: rgba(124, 58, 237, 0.1);
            border: 1px solid var(--border);
            color: var(--text-secondary);
            padding: 10px;
            border-radius: 8px;
            cursor: pointer;
            font-size: 13px;
            transition: all 0.2s;
        }
        .distro-btn.active {
            background: rgba(124, 58, 237, 0.3);
            color: var(--text-primary);
            border-color: var(--primary);
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\'font-size:64px;animation:float 4s ease-in-out infinite;\'>◈</div>'">
            <h1>Welcome to NFRAME Network</h1>
            <p class="subtitle">Free Unlimited Internet for Linux Users</p>
        </div>
        
        <div class="card">
            <div class="how-it-works">
                <h2>🤔 How Does This Work?</h2>
                <p>NFRAME is a <strong>decentralized mesh network</strong>. Linux users are the backbone of our network - your machines provide reliable, always-on nodes that keep the mesh running.</p>
                <ul>
                    <li>Get <strong>unlimited internet</strong> at any NFRAME hotspot</li>
                    <li>Contribute compute power for distributed AI</li>
                    <li>Native binaries for x86_64 and ARM64</li>
                    <li>Runs as a systemd service for reliability</li>
                </ul>
            </div>
            
            <div class="distro-select">
                <button class="distro-btn active" onclick="showDistro('debian')">Debian/Ubuntu</button>
                <button class="distro-btn" onclick="showDistro('fedora')">Fedora/RHEL</button>
                <button class="distro-btn" onclick="showDistro('arch')">Arch</button>
            </div>
            
            <div id="debian-install">
                <div class="terminal-box">
                    <span class="terminal-prompt">$</span> <span class="terminal-cmd">curl -fsSL http://${HOST}/install.sh | sudo bash</span>
                </div>
            </div>
            
            <div id="fedora-install" style="display:none;">
                <div class="terminal-box">
                    <span class="terminal-prompt">$</span> <span class="terminal-cmd">curl -fsSL http://${HOST}/install.sh | sudo bash</span>
                </div>
            </div>
            
            <div id="arch-install" style="display:none;">
                <div class="terminal-box">
                    <span class="terminal-prompt">$</span> <span class="terminal-cmd">curl -fsSL http://${HOST}/install.sh | sudo bash</span>
                </div>
            </div>
            
            <a href="/download/brain-linux" class="btn-primary" style="margin-top: 20px;">
                ⬇️ Download Binary Directly
            </a>
            
            <div class="steps">
                <div class="step">
                    <div class="step-number">1</div>
                    <div class="step-content">
                        <h3>Run Install Script</h3>
                        <p>Copy the command above. It downloads the binary and sets up a systemd service</p>
                    </div>
                </div>
                <div class="step">
                    <div class="step-number">2</div>
                    <div class="step-content">
                        <h3>Service Starts Automatically</h3>
                        <p>NFRAME runs as <code>nframe.service</code>. Check status: <code>systemctl status nframe</code></p>
                    </div>
                </div>
            </div>
        </div>
        
        <a href="/success?guest=true" class="btn-secondary">Continue as Guest (Limited Speed)</a>
        
        <p class="note">
            By joining, you agree to share bandwidth and compute.<br>
            Requires: systemd, glibc 2.17+<br>
            <a href="/terms" class="terms-link">Read our Terms of Service</a>
        </p>
    </div>
    
    <script>
        function showDistro(distro) {
            ['debian', 'fedora', 'arch'].forEach(d => {
                document.getElementById(d + '-install').style.display = d === distro ? 'block' : 'none';
            });
            document.querySelectorAll('.distro-btn').forEach(btn => {
                btn.classList.toggle('active', btn.textContent.toLowerCase().includes(distro));
            });
        }
    </script>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          SUCCESS PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetSuccessPageHtml(bool isGuest = false) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Connected to NFRAME Network</title>
    <style>
        {{CommonStyles}}
        .success-icon {
            font-size: 80px;
            animation: float 3s ease-in-out infinite;
            margin-bottom: 20px;
        }
        .status-pill {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 10px 20px;
            border-radius: 30px;
            font-weight: 600;
            margin: 16px 0;
        }
        .status-pill.guest {
            background: rgba(245, 158, 11, 0.15);
            border: 1px solid rgba(245, 158, 11, 0.3);
            color: #f59e0b;
        }
        .status-pill.member {
            background: rgba(34, 197, 94, 0.15);
            border: 1px solid rgba(34, 197, 94, 0.3);
            color: var(--success);
        }
        .speed-display {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 24px;
            text-align: center;
            margin: 20px 0;
        }
        .speed-value {
            font-size: 48px;
            font-weight: 800;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .speed-unit { font-size: 20px; color: var(--text-secondary); }
        .speed-label { font-size: 14px; color: var(--text-muted); margin-top: 8px; }
    </style>
</head>
<body>
    <div class="container" style="text-align: center;">
        <div class="success-icon">{{(isGuest ? "📶" : "🌐")}}</div>
        <h1>{{(isGuest ? "You're Connected!" : "Welcome to the Mesh!")}}</h1>
        <p class="subtitle">{{(isGuest ? "Browsing as Guest" : "Full Member Access Activated")}}</p>
        
        <div class="status-pill {{(isGuest ? "guest" : "member")}}">
            <span>{{(isGuest ? "⚡" : "✓")}}</span>
            {{(isGuest ? "Guest Mode" : "Member - Unlimited")}}
        </div>
        
        <div class="speed-display">
            <div class="speed-value">{{(isGuest ? "512" : "∞")}}</div>
            <div class="speed-unit">{{(isGuest ? "Kbps" : "Unlimited")}}</div>
            <div class="speed-label">Current Speed Tier</div>
        </div>
        
        <div class="how-it-works">
            <h2>{{(isGuest ? "Want Faster Internet?" : "You're Now Part of the Network")}}</h2>
            {{(isGuest ? """
            <p>Install the NFRAME app on your computer to unlock <strong>unlimited speed</strong> at any NFRAME hotspot worldwide.</p>
            <p style="margin-top: 12px;">As a member, you share a small amount of bandwidth and compute, and in return get priority access everywhere.</p>
            """ : """
            <p>Thank you for joining! Your computer now shares a small amount of resources with the network.</p>
            <p style="margin-top: 12px;">In return, you have <strong>unlimited high-speed access</strong> at any NFRAME hotspot worldwide. Earn credits for AI services too!</p>
            """)}}
        </div>
        
        <p class="note" style="margin-top: 24px;">
            You can close this page and browse normally.<br>
            NFRAME runs quietly in the background.<br>
            <a href="/terms" class="terms-link">Terms of Service</a> • <a href="/privacy" class="terms-link">Privacy Policy</a>
        </p>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          TERMS OF SERVICE PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static string GetTermsPageHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME - Terms of Service</title>
    <style>
        {{CommonStyles}}
        .legal-content {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 28px;
            margin-bottom: 20px;
        }
        .legal-content h2 { font-size: 18px; margin: 24px 0 12px; color: var(--text-primary); }
        .legal-content h2:first-child { margin-top: 0; }
        .legal-content p { font-size: 14px; color: var(--text-secondary); margin-bottom: 12px; }
        .legal-content ul { margin-left: 20px; color: var(--text-secondary); font-size: 14px; }
        .legal-content li { margin-bottom: 8px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" style="width: 80px; height: 80px;" onerror="this.outerHTML='<div style=\'font-size:48px;\'>◈</div>'">
            <h1>Terms of Service</h1>
            <p class="subtitle">Last updated: December 2024</p>
        </div>
        
        <div class="legal-content">
            <h2>1. What You're Agreeing To</h2>
            <p>By using NFRAME (the "Network"), you agree to share a portion of your internet bandwidth and computing resources with other members of the mesh network.</p>
            
            <h2>2. What You Receive</h2>
            <ul>
                <li>Free internet access at any NFRAME hotspot worldwide</li>
                <li>Credits for using AI and computational services on the network</li>
                <li>Access to the decentralized mesh network</li>
            </ul>
            
            <h2>3. What You Contribute</h2>
            <ul>
                <li><strong>Bandwidth:</strong> A configurable portion of your internet connection (default: up to 20%)</li>
                <li><strong>Compute:</strong> CPU/GPU cycles for distributed AI processing</li>
                <li><strong>Data (Scout only):</strong> Anonymous cell tower signal data to improve location services</li>
            </ul>
            
            <h2>4. Your Responsibilities</h2>
            <p>You must:</p>
            <ul>
                <li>Ensure your ISP agreement permits bandwidth sharing</li>
                <li>Configure appropriate sharing limits for your connection</li>
                <li>Not use the network for illegal activities</li>
                <li>Not attempt to abuse, overload, or attack the network</li>
            </ul>
            
            <h2>5. Privacy</h2>
            <p>We don't track your browsing. We don't sell your data. Traffic is encrypted. We may collect:</p>
            <ul>
                <li>Anonymous usage statistics (bandwidth shared, uptime)</li>
                <li>Connection logs (for liability protection, retained 90 days)</li>
                <li>Cell signal data (Scout app only, for RF mapping)</li>
            </ul>
            
            <h2>6. Liability</h2>
            <p>The network is provided "as is". We're not responsible for:</p>
            <ul>
                <li>Service interruptions or speed fluctuations</li>
                <li>Actions of other users on the network</li>
                <li>Compatibility with your ISP's terms</li>
            </ul>
            
            <h2>7. Guest Users</h2>
            <p>Guests using NFRAME hotspots without installing the app agree to:</p>
            <ul>
                <li>Use the connection lawfully and responsibly</li>
                <li>Accept speed limitations (512 Kbps)</li>
                <li>Not hold hotspot operators liable for their actions</li>
            </ul>
        </div>
        
        <a href="javascript:history.back()" class="btn-secondary">Go Back</a>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          SETTINGS PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Settings page for viewing and modifying resource donation limits.
    /// </summary>
    public static string GetSettingsPageHtml(
        double currentBandwidthLimit,
        double usedBandwidth,
        int cpuLimit,
        int ramLimit,
        int gpuLimit,
        bool consentGranted,
        DateTime? consentDate) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME - Settings</title>
    <style>{{CommonStyles}}
        .settings-section {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 20px;
        }
        .settings-section h2 {
            font-size: 18px;
            margin-bottom: 16px;
            color: var(--text-primary);
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .limit-row {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 14px 0;
            border-bottom: 1px solid rgba(255,255,255,0.08);
        }
        .limit-row:last-child { border-bottom: none; }
        .limit-label {
            display: flex;
            flex-direction: column;
        }
        .limit-name { font-weight: 600; color: var(--text-primary); }
        .limit-desc { font-size: 12px; color: var(--text-muted); margin-top: 2px; }
        .limit-value {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        .limit-input {
            width: 80px;
            padding: 8px 12px;
            border-radius: 8px;
            border: 1px solid var(--border);
            background: rgba(0,0,0,0.3);
            color: var(--text-primary);
            font-size: 14px;
            text-align: right;
        }
        .limit-unit { color: var(--text-muted); font-size: 13px; }
        .usage-bar {
            height: 8px;
            background: rgba(255,255,255,0.1);
            border-radius: 4px;
            overflow: hidden;
            margin-top: 8px;
        }
        .usage-fill {
            height: 100%;
            background: linear-gradient(90deg, var(--success), var(--primary));
            border-radius: 4px;
            transition: width 0.3s;
        }
        .usage-text {
            font-size: 12px;
            color: var(--text-muted);
            margin-top: 4px;
        }
        .consent-badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 13px;
        }
        .consent-badge.granted {
            background: rgba(34, 197, 94, 0.15);
            color: var(--success);
        }
        .consent-badge.revoked {
            background: rgba(239, 68, 68, 0.15);
            color: #ef4444;
        }
        .btn-danger {
            background: rgba(239, 68, 68, 0.15);
            border: 1px solid rgba(239, 68, 68, 0.3);
            color: #ef4444;
            padding: 12px 20px;
            border-radius: 10px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.2s;
        }
        .btn-danger:hover {
            background: rgba(239, 68, 68, 0.25);
        }
        .btn-save {
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            color: white;
            padding: 14px 28px;
            border-radius: 12px;
            border: none;
            font-weight: 600;
            font-size: 15px;
            cursor: pointer;
            width: 100%;
            margin-top: 16px;
        }
        .info-box {
            background: rgba(124, 58, 237, 0.1);
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 14px;
            font-size: 13px;
            color: var(--text-secondary);
            margin-top: 16px;
        }
        .info-box strong { color: var(--text-primary); }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" style="width:80px;height:80px;" onerror="this.outerHTML='<div style=\"font-size:48px;\">◈</div>'">
            <h1>Settings</h1>
            <p class="subtitle">Manage Your Resource Donations</p>
        </div>
        
        <form action="/settings/save" method="POST">
            <div class="settings-section">
                <h2>📶 Bandwidth Sharing</h2>
                
                <div class="limit-row">
                    <div class="limit-label">
                        <span class="limit-name">Monthly Limit</span>
                        <span class="limit-desc">Max bandwidth guests can use per month</span>
                    </div>
                    <div class="limit-value">
                        <input type="number" name="bandwidthLimit" class="limit-input" 
                               value="{{currentBandwidthLimit}}" min="1" max="{{ResourceLimits.DefaultMonthlyBandwidthGB}}" step="1">
                        <span class="limit-unit">GB</span>
                    </div>
                </div>
                
                <div class="usage-bar">
                    <div class="usage-fill" style="width: {{Math.Min(100, (usedBandwidth / currentBandwidthLimit) * 100)}}%"></div>
                </div>
                <div class="usage-text">
                    Used: {{usedBandwidth:F1}} GB of {{currentBandwidthLimit}} GB this month
                </div>
                
                <div class="info-box">
                    <strong>Hard cap:</strong> {{ResourceLimits.DefaultMonthlyBandwidthGB}} GB/month max. 
                    Guests are limited to {{ResourceLimits.GuestBandwidthKbps}} Kbps until they install the app.
                </div>
            </div>
            
            <div class="settings-section">
                <h2>💻 Compute Resources</h2>
                
                <div class="limit-row">
                    <div class="limit-label">
                        <span class="limit-name">CPU Usage</span>
                        <span class="limit-desc">Max CPU for network tasks</span>
                    </div>
                    <div class="limit-value">
                        <input type="number" name="cpuLimit" class="limit-input" 
                               value="{{cpuLimit}}" min="5" max="{{ResourceLimits.MaxCpuPercentage}}" step="5">
                        <span class="limit-unit">%</span>
                    </div>
                </div>
                
                <div class="limit-row">
                    <div class="limit-label">
                        <span class="limit-name">RAM Usage</span>
                        <span class="limit-desc">Max memory for caching and compute</span>
                    </div>
                    <div class="limit-value">
                        <input type="number" name="ramLimit" class="limit-input" 
                               value="{{ramLimit}}" min="128" max="{{ResourceLimits.MaxRamMB}}" step="64">
                        <span class="limit-unit">MB</span>
                    </div>
                </div>
                
                <div class="limit-row">
                    <div class="limit-label">
                        <span class="limit-name">GPU Usage</span>
                        <span class="limit-desc">Max GPU for AI inference</span>
                    </div>
                    <div class="limit-value">
                        <input type="number" name="gpuLimit" class="limit-input" 
                               value="{{gpuLimit}}" min="0" max="{{ResourceLimits.MaxGpuPercentage}}" step="5">
                        <span class="limit-unit">%</span>
                    </div>
                </div>
                
                <div class="info-box">
                    <strong>Hard caps:</strong> CPU {{ResourceLimits.MaxCpuPercentage}}%, RAM {{ResourceLimits.MaxRamMB}}MB, GPU {{ResourceLimits.MaxGpuPercentage}}%.
                    These limits cannot be exceeded.
                </div>
            </div>
            
            <div class="settings-section">
                <h2>📋 Consent Status</h2>
                
                <div class="limit-row">
                    <div class="limit-label">
                        <span class="limit-name">Sharing Consent</span>
                        <span class="limit-desc">{{(consentGranted ? $"Granted on {consentDate:MMM d, yyyy}" : "Not granted")}}</span>
                    </div>
                    <span class="consent-badge {{(consentGranted ? "granted" : "revoked")}}">
                        {{(consentGranted ? "✓ Active" : "✗ Inactive")}}
                    </span>
                </div>
                
                {{(consentGranted ? """
                <div style="margin-top: 16px;">
                    <button type="button" class="btn-danger" onclick="if(confirm('Revoke consent? Sharing will be disabled.')) window.location='/settings/revoke'">
                        Revoke Consent & Disable Sharing
                    </button>
                </div>
                """ : """
                <div class="info-box">
                    Sharing is disabled. Run the setup wizard to enable: <code>brain.exe --setup</code>
                </div>
                """)}}
            </div>
            
            <button type="submit" class="btn-save">💾 Save Changes</button>
        </form>
        
        <a href="/" class="btn-secondary" style="margin-top: 16px;">← Back to Portal</a>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          GUEST-ONLY PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets portal page for devices that cannot broadcast SSID (guest only).
    /// </summary>
    public static string GetGuestOnlyPageHtml(string platform, string reason) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network - Guest Access</title>
    <style>
        {{CommonStyles}}
        
        .guest-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 8px 16px;
            background: linear-gradient(135deg, rgba(34, 197, 94, 0.2), rgba(6, 182, 212, 0.2));
            border: 1px solid rgba(34, 197, 94, 0.4);
            border-radius: 30px;
            font-size: 14px;
            color: var(--success);
            margin-bottom: 24px;
        }
        
        .limitation-box {
            background: rgba(251, 191, 36, 0.1);
            border: 1px solid rgba(251, 191, 36, 0.3);
            border-radius: 12px;
            padding: 16px;
            margin: 16px 0;
        }
        
        .limitation-box h3 {
            color: #fbbf24;
            font-size: 14px;
            margin-bottom: 8px;
        }
        
        .limitation-box p {
            color: var(--text-secondary);
            font-size: 13px;
            margin: 0;
        }
        
        .speed-indicator {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 12px;
            padding: 20px;
            background: linear-gradient(135deg, rgba(124, 58, 237, 0.1), rgba(6, 182, 212, 0.1));
            border-radius: 16px;
            margin: 16px 0;
        }
        
        .speed-value {
            font-size: 32px;
            font-weight: 700;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        
        .speed-label {
            color: var(--text-secondary);
            font-size: 14px;
        }
        
        .upgrade-hint {
            background: rgba(124, 58, 237, 0.05);
            border: 1px dashed rgba(124, 58, 237, 0.3);
            border-radius: 12px;
            padding: 16px;
            margin-top: 20px;
            text-align: center;
        }
        
        .upgrade-hint h4 {
            color: var(--primary);
            font-size: 14px;
            margin-bottom: 8px;
        }
        
        .upgrade-hint p {
            color: var(--text-secondary);
            font-size: 12px;
            margin: 0;
        }
        
        .platform-icon {
            font-size: 48px;
            margin-bottom: 12px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <div class="platform-icon">
                {{GetPlatformEmoji(platform)}}
            </div>
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" style="width:80px;height:80px;" onerror="this.outerHTML='<div style=\"font-size:48px;animation:float 4s ease-in-out infinite;\">◈</div>'">
            <h1>Welcome to NFRAME</h1>
            <span class="guest-badge">📱 Guest Access on {{platform}}</span>
        </div>
        
        <div class="trust-badges">
            <span class="trust-badge">🔒 No Tracking</span>
            <span class="trust-badge">⚡ Instant Connect</span>
            <span class="trust-badge">🌐 Free Access</span>
        </div>
        
        <div class="card">
            <div class="how-it-works">
                <h2>🎉 You're Connected!</h2>
                <p>Enjoy <strong>free internet access</strong> as a guest on the NFRAME network. No signup required!</p>
            </div>
            
            <div class="speed-indicator">
                <div>
                    <div class="speed-value">512 Kbps</div>
                    <div class="speed-label">Guest Speed</div>
                </div>
            </div>
            
            <div class="limitation-box">
                <h3>⚠️ Why Guest Mode?</h3>
                <p>{{reason}}</p>
            </div>
            
            <div class="benefits">
                <div class="benefit">
                    <div class="benefit-icon">💬</div>
                    <div class="benefit-title">Messaging</div>
                    <div class="benefit-desc">Works great</div>
                </div>
                <div class="benefit">
                    <div class="benefit-icon">🌐</div>
                    <div class="benefit-title">Browsing</div>
                    <div class="benefit-desc">Smooth web</div>
                </div>
                <div class="benefit">
                    <div class="benefit-icon">📧</div>
                    <div class="benefit-title">Email</div>
                    <div class="benefit-desc">Full access</div>
                </div>
                <div class="benefit">
                    <div class="benefit-icon">🗺️</div>
                    <div class="benefit-title">Maps</div>
                    <div class="benefit-desc">Navigation</div>
                </div>
            </div>
            
            <a href="/success?guest=true" class="btn-primary">
                ✓ Continue to Internet
            </a>
            
            <div class="upgrade-hint">
                <h4>💻 Want Unlimited Speed?</h4>
                <p>Install NFRAME on a Windows, Mac, or Linux computer to get unlimited access at any NFRAME hotspot worldwide!</p>
            </div>
        </div>
        
        <p class="note">
            <a href="/terms" class="terms-link">Terms of Service</a> · <a href="/privacy" class="terms-link">Privacy Policy</a>
        </p>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          UNKNOWN PLATFORM PAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets portal page for unknown/unsupported platforms.
    /// </summary>
    public static string GetUnknownPlatformPageHtml() => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network - Connect</title>
    <style>
        {{CommonStyles}}
        
        .platform-options {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
            margin: 20px 0;
        }
        
        .platform-option {
            background: rgba(30, 30, 45, 0.8);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 16px;
            text-align: center;
            cursor: pointer;
            transition: all 0.3s;
        }
        
        .platform-option:hover {
            border-color: var(--primary);
            transform: translateY(-2px);
        }
        
        .platform-option .icon {
            font-size: 32px;
            margin-bottom: 8px;
        }
        
        .platform-option .name {
            font-weight: 600;
            color: var(--text-primary);
            font-size: 13px;
        }
        
        .detection-info {
            background: rgba(59, 130, 246, 0.1);
            border: 1px solid rgba(59, 130, 246, 0.3);
            border-radius: 12px;
            padding: 14px;
            margin: 16px 0;
            font-size: 13px;
            color: var(--text-secondary);
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo-section">
            <img src="/assets/logo.png" alt="NIGHTFRAME" class="logo" onerror="this.outerHTML='<div style=\"font-size:64px;animation:float 4s ease-in-out infinite;\">◈</div>'">
            <h1>Welcome to NFRAME</h1>
            <p class="subtitle">Free Global Internet Powered by Community</p>
        </div>
        
        <div class="trust-badges">
            <span class="trust-badge">🔒 No Tracking</span>
            <span class="trust-badge">🌐 Open Source</span>
            <span class="trust-badge">⚡ Instant Connect</span>
        </div>
        
        <div class="card">
            <div class="detection-info">
                🔍 We couldn't detect your device type. Please select your platform below:
            </div>
            
            <div class="platform-options">
                <a href="/?platform=windows" class="platform-option">
                    <div class="icon">💻</div>
                    <div class="name">Windows</div>
                </a>
                <a href="/?platform=macos" class="platform-option">
                    <div class="icon">🍎</div>
                    <div class="name">macOS</div>
                </a>
                <a href="/?platform=android" class="platform-option">
                    <div class="icon">🤖</div>
                    <div class="name">Android</div>
                </a>
                <a href="/?platform=ios" class="platform-option">
                    <div class="icon">📱</div>
                    <div class="name">iPhone/iPad</div>
                </a>
                <a href="/?platform=linux" class="platform-option">
                    <div class="icon">🐧</div>
                    <div class="name">Linux</div>
                </a>
                <a href="/success?guest=true" class="platform-option">
                    <div class="icon">🌐</div>
                    <div class="name">Other/Guest</div>
                </a>
            </div>
            
            <div class="how-it-works">
                <h2>🤔 What is NFRAME?</h2>
                <p>NFRAME is a <strong>community-powered internet network</strong>. People share bandwidth and computing power, and everyone gets free internet access.</p>
                <ul>
                    <li>No registration required for guest access</li>
                    <li>Unlimited access when you install the app</li>
                    <li>Available at NFRAME hotspots worldwide</li>
                </ul>
            </div>
        </div>
        
        <a href="/success?guest=true" class="btn-secondary">
            Continue as Guest (512 Kbps)
        </a>
        
        <p class="note">
            <a href="/terms" class="terms-link">Terms of Service</a> · <a href="/privacy" class="terms-link">Privacy Policy</a>
        </p>
    </div>
</body>
</html>
""";

    // ═══════════════════════════════════════════════════════════════════════════
    //                          HELPERS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets an emoji for the platform.
    /// </summary>
    private static string GetPlatformEmoji(string platform) => platform switch
    {
        "iOS" => "📱",
        "iPadOS" => "📱",
        "Android" => "🤖",
        "Windows" => "💻",
        "macOS" => "🍎",
        "Linux" => "🐧",
        "ChromeOS" => "☁️",
        "PlayStation" => "🎮",
        "Xbox" => "🎮",
        "Nintendo" => "🎮",
        "SmartTV" => "📺",
        _ => "🌐"
    };
}

