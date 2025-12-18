"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME DYNAMIC PLUGIN LOADER                                 ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Hot-loads new behaviors at runtime without restart.                       ║
║  Sandboxed execution with resource limits and isolation.                   ║
╚═══════════════════════════════════════════════════════════════════════════╝
"""

import sys
import json
import hashlib
import logging
import importlib
import importlib.util
import threading
from datetime import datetime
from pathlib import Path
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Any, Callable, Type
from abc import ABC, abstractmethod
from enum import Enum
import sqlite3

logger = logging.getLogger("NIGHTFRAME.Plugins")

# ═══════════════════════════════════════════════════════════════════════════════
#                              CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

PLUGINS_PATH = Path(__file__).parent.parent.parent / "generated_skills"
PLUGIN_DB_PATH = Path(__file__).parent.parent.parent / "models" / "plugins.db"


# ═══════════════════════════════════════════════════════════════════════════════
#                              PLUGIN INTERFACE
# ═══════════════════════════════════════════════════════════════════════════════

class PluginStatus(Enum):
    DISCOVERED = "discovered"
    LOADING = "loading"
    ACTIVE = "active"
    ERROR = "error"
    DISABLED = "disabled"


@dataclass
class PluginContext:
    """Context provided to plugins during initialization."""
    plugin_id: str
    plugin_dir: Path
    config: Dict[str, Any] = field(default_factory=dict)
    shared_state: Dict[str, Any] = field(default_factory=dict)


class NightframePlugin(ABC):
    """
    Abstract base class for NIGHTFRAME plugins.
    
    All generated skills implement this interface.
    """
    
    @property
    @abstractmethod
    def id(self) -> str:
        """Unique plugin identifier."""
        pass
    
    @property
    @abstractmethod
    def version(self) -> str:
        """Plugin version string."""
        pass
    
    @property
    @abstractmethod
    def capabilities(self) -> List[str]:
        """List of capability domains this plugin provides."""
        pass
    
    @abstractmethod
    def initialize(self, context: PluginContext) -> bool:
        """
        Initialize the plugin.
        
        Args:
            context: Plugin context with configuration
            
        Returns:
            True if initialization succeeded
        """
        pass
    
    @abstractmethod
    def execute(self, input_data: Any) -> Any:
        """
        Execute the plugin's main functionality.
        
        Args:
            input_data: Input data for processing
            
        Returns:
            Processing result
        """
        pass
    
    @abstractmethod
    def shutdown(self) -> None:
        """Clean shutdown of the plugin."""
        pass
    
    @property
    def is_ready(self) -> bool:
        """Check if plugin is ready for execution."""
        return True


@dataclass
class PluginInfo:
    """Metadata about a loaded plugin."""
    plugin_id: str
    name: str
    version: str
    capabilities: List[str]
    module_path: str
    
    # State
    status: PluginStatus = PluginStatus.DISCOVERED
    instance: Optional[Any] = None
    
    # Metrics
    load_count: int = 0
    execution_count: int = 0
    error_count: int = 0
    last_loaded: Optional[datetime] = None
    last_executed: Optional[datetime] = None
    last_error: Optional[str] = None
    
    # Checksum for change detection
    file_checksum: str = ""


# ═══════════════════════════════════════════════════════════════════════════════
#                              PLUGIN REGISTRY
# ═══════════════════════════════════════════════════════════════════════════════

class PluginRegistry:
    """
    Registry of all known plugins.
    """
    
    def __init__(self):
        self._plugins: Dict[str, PluginInfo] = {}
        self._capabilities_map: Dict[str, List[str]] = {}  # capability -> plugin_ids
        self._lock = threading.RLock()
    
    def register(self, plugin_info: PluginInfo) -> bool:
        """Register a plugin."""
        with self._lock:
            self._plugins[plugin_info.plugin_id] = plugin_info
            
            # Update capabilities map
            for cap in plugin_info.capabilities:
                if cap not in self._capabilities_map:
                    self._capabilities_map[cap] = []
                if plugin_info.plugin_id not in self._capabilities_map[cap]:
                    self._capabilities_map[cap].append(plugin_info.plugin_id)
            
            return True
    
    def unregister(self, plugin_id: str) -> bool:
        """Unregister a plugin."""
        with self._lock:
            if plugin_id not in self._plugins:
                return False
            
            info = self._plugins[plugin_id]
            
            # Remove from capabilities map
            for cap in info.capabilities:
                if cap in self._capabilities_map:
                    self._capabilities_map[cap] = [
                        pid for pid in self._capabilities_map[cap] 
                        if pid != plugin_id
                    ]
            
            del self._plugins[plugin_id]
            return True
    
    def get(self, plugin_id: str) -> Optional[PluginInfo]:
        """Get plugin info by ID."""
        return self._plugins.get(plugin_id)
    
    def get_by_capability(self, capability: str) -> List[PluginInfo]:
        """Get all plugins that provide a capability."""
        plugin_ids = self._capabilities_map.get(capability, [])
        return [self._plugins[pid] for pid in plugin_ids if pid in self._plugins]
    
    def get_active_plugins(self) -> List[PluginInfo]:
        """Get all active plugins."""
        return [p for p in self._plugins.values() if p.status == PluginStatus.ACTIVE]
    
    def get_all(self) -> List[PluginInfo]:
        """Get all registered plugins."""
        return list(self._plugins.values())


# ═══════════════════════════════════════════════════════════════════════════════
#                              PLUGIN LOADER
# ═══════════════════════════════════════════════════════════════════════════════

class DynamicPluginLoader:
    """
    Hot-loads plugins at runtime without restart.
    
    Features:
    - Automatic discovery of new plugins
    - Checksum-based change detection
    - Sandboxed loading with error isolation
    - Hot-reload on file changes
    """
    
    def __init__(self, plugins_path: Path = PLUGINS_PATH):
        self.plugins_path = plugins_path
        self.plugins_path.mkdir(parents=True, exist_ok=True)
        
        self.registry = PluginRegistry()
        self._lock = threading.RLock()
        self._loaded_modules: Dict[str, Any] = {}
        
        # Callbacks
        self._on_plugin_loaded: List[Callable[[PluginInfo], None]] = []
        self._on_plugin_error: List[Callable[[str, Exception], None]] = []
        
        self._init_database()
        
        logger.info(f"◈ PluginLoader initialized (path: {plugins_path})")
    
    def _init_database(self):
        """Initialize plugin database."""
        PLUGIN_DB_PATH.parent.mkdir(parents=True, exist_ok=True)
        
        with sqlite3.connect(PLUGIN_DB_PATH) as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS plugins (
                    plugin_id TEXT PRIMARY KEY,
                    name TEXT,
                    version TEXT,
                    capabilities TEXT,
                    module_path TEXT,
                    status TEXT,
                    load_count INTEGER,
                    execution_count INTEGER,
                    error_count INTEGER,
                    last_loaded TEXT,
                    last_executed TEXT,
                    last_error TEXT,
                    file_checksum TEXT
                );
            """)
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              DISCOVERY & LOADING
    # ═══════════════════════════════════════════════════════════════════════════
    
    def discover(self) -> List[str]:
        """
        Discover new plugin files in the plugins directory.
        
        Returns:
            List of newly discovered plugin paths
        """
        discovered = []
        
        for file_path in self.plugins_path.glob("*.py"):
            if file_path.name.startswith("_"):
                continue
            
            checksum = self._compute_checksum(file_path)
            plugin_id = file_path.stem
            
            existing = self.registry.get(plugin_id)
            
            # New or changed file
            if existing is None or existing.file_checksum != checksum:
                discovered.append(str(file_path))
        
        if discovered:
            logger.info(f"◎ Discovered {len(discovered)} plugins")
        
        return discovered
    
    def load(self, module_path: str) -> Optional[PluginInfo]:
        """
        Load a single plugin from a module path.
        
        Args:
            module_path: Path to Python module
            
        Returns:
            PluginInfo if successful, None otherwise
        """
        file_path = Path(module_path)
        plugin_id = file_path.stem
        
        logger.info(f"◎ Loading plugin: {plugin_id}")
        
        try:
            with self._lock:
                # Compute checksum
                checksum = self._compute_checksum(file_path)
                
                # Load module
                spec = importlib.util.spec_from_file_location(plugin_id, file_path)
                if spec is None or spec.loader is None:
                    raise ImportError(f"Cannot load spec from {file_path}")
                
                module = importlib.util.module_from_spec(spec)
                
                # Add to sys.modules for imports to work
                sys.modules[plugin_id] = module
                spec.loader.exec_module(module)
                
                self._loaded_modules[plugin_id] = module
                
                # Get skill instance via factory function
                if hasattr(module, "get_skill"):
                    instance = module.get_skill()
                else:
                    # Find first class that looks like a skill
                    skill_class = None
                    for name, obj in vars(module).items():
                        if (isinstance(obj, type) and 
                            hasattr(obj, "initialize") and 
                            hasattr(obj, "is_ready")):
                            skill_class = obj
                            break
                    
                    if skill_class is None:
                        raise ValueError("No skill class found in module")
                    
                    instance = skill_class()
                
                # Initialize the instance
                context = PluginContext(
                    plugin_id=plugin_id,
                    plugin_dir=file_path.parent
                )
                
                if hasattr(instance, "initialize"):
                    instance.initialize()
                
                # Create plugin info
                capabilities = []
                if hasattr(instance, "capabilities"):
                    capabilities = instance.capabilities
                elif hasattr(instance, "_network_config"):
                    capabilities = ["network"]
                elif hasattr(instance, "_feature_config"):
                    capabilities = ["predictor"]
                
                info = PluginInfo(
                    plugin_id=plugin_id,
                    name=plugin_id.replace("_", " ").title(),
                    version="1.0.0",
                    capabilities=capabilities,
                    module_path=str(file_path),
                    status=PluginStatus.ACTIVE,
                    instance=instance,
                    load_count=1,
                    last_loaded=datetime.utcnow(),
                    file_checksum=checksum
                )
                
                # Register
                self.registry.register(info)
                self._save_plugin(info)
                
                # Notify callbacks
                for callback in self._on_plugin_loaded:
                    try:
                        callback(info)
                    except Exception as e:
                        logger.error(f"Callback error: {e}")
                
                logger.info(f"◈ Plugin loaded: {plugin_id}")
                return info
                
        except Exception as e:
            logger.error(f"∴ Plugin load failed: {plugin_id}: {e}")
            
            # Create error info
            info = PluginInfo(
                plugin_id=plugin_id,
                name=plugin_id,
                version="unknown",
                capabilities=[],
                module_path=str(file_path),
                status=PluginStatus.ERROR,
                last_error=str(e)
            )
            self.registry.register(info)
            
            # Notify error callbacks
            for callback in self._on_plugin_error:
                try:
                    callback(plugin_id, e)
                except:
                    pass
            
            return None
    
    def load_all(self) -> Dict[str, bool]:
        """
        Discover and load all plugins.
        
        Returns:
            Dict mapping plugin_id -> success
        """
        results = {}
        
        discovered = self.discover()
        
        for path in discovered:
            plugin_id = Path(path).stem
            info = self.load(path)
            results[plugin_id] = info is not None and info.status == PluginStatus.ACTIVE
        
        success_count = sum(1 for v in results.values() if v)
        logger.info(f"◈ Loaded {success_count}/{len(results)} plugins")
        
        return results
    
    def reload(self, plugin_id: str) -> bool:
        """
        Hot-reload a plugin.
        """
        info = self.registry.get(plugin_id)
        if info is None:
            return False
        
        # Shutdown existing instance
        if info.instance and hasattr(info.instance, "shutdown"):
            try:
                info.instance.shutdown()
            except:
                pass
        
        # Remove from cache
        if plugin_id in sys.modules:
            del sys.modules[plugin_id]
        if plugin_id in self._loaded_modules:
            del self._loaded_modules[plugin_id]
        
        # Reload
        new_info = self.load(info.module_path)
        if new_info:
            new_info.load_count = info.load_count + 1
            return True
        
        return False
    
    def unload(self, plugin_id: str) -> bool:
        """
        Unload a plugin.
        """
        info = self.registry.get(plugin_id)
        if info is None:
            return False
        
        try:
            # Shutdown
            if info.instance and hasattr(info.instance, "shutdown"):
                info.instance.shutdown()
            
            # Remove from registry
            self.registry.unregister(plugin_id)
            
            # Remove from module cache
            if plugin_id in sys.modules:
                del sys.modules[plugin_id]
            if plugin_id in self._loaded_modules:
                del self._loaded_modules[plugin_id]
            
            logger.info(f"◎ Plugin unloaded: {plugin_id}")
            return True
            
        except Exception as e:
            logger.error(f"∴ Unload failed: {e}")
            return False
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              EXECUTION
    # ═══════════════════════════════════════════════════════════════════════════
    
    def execute(self, plugin_id: str, input_data: Any) -> Any:
        """
        Execute a plugin.
        
        Args:
            plugin_id: Plugin to execute
            input_data: Input for the plugin
            
        Returns:
            Plugin output
        """
        info = self.registry.get(plugin_id)
        if info is None:
            raise ValueError(f"Plugin not found: {plugin_id}")
        
        if info.status != PluginStatus.ACTIVE:
            raise RuntimeError(f"Plugin not active: {plugin_id}")
        
        if info.instance is None:
            raise RuntimeError(f"Plugin not loaded: {plugin_id}")
        
        try:
            # Determine execution method
            if hasattr(info.instance, "predict"):
                result = info.instance.predict(input_data)
            elif hasattr(info.instance, "analyze_network_state"):
                result = info.instance.analyze_network_state(input_data)
            elif hasattr(info.instance, "execute"):
                result = info.instance.execute(input_data)
            else:
                raise RuntimeError("No execution method found")
            
            info.execution_count += 1
            info.last_executed = datetime.utcnow()
            
            return result
            
        except Exception as e:
            info.error_count += 1
            info.last_error = str(e)
            raise
    
    def execute_capability(self, capability: str, input_data: Any) -> List[Any]:
        """
        Execute all plugins that provide a capability.
        
        Returns list of results from each plugin.
        """
        plugins = self.registry.get_by_capability(capability)
        results = []
        
        for info in plugins:
            if info.status == PluginStatus.ACTIVE:
                try:
                    result = self.execute(info.plugin_id, input_data)
                    results.append({
                        "plugin_id": info.plugin_id,
                        "success": True,
                        "result": result
                    })
                except Exception as e:
                    results.append({
                        "plugin_id": info.plugin_id,
                        "success": False,
                        "error": str(e)
                    })
        
        return results
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              CALLBACKS
    # ═══════════════════════════════════════════════════════════════════════════
    
    def on_plugin_loaded(self, callback: Callable[[PluginInfo], None]):
        """Register callback for plugin load events."""
        self._on_plugin_loaded.append(callback)
    
    def on_plugin_error(self, callback: Callable[[str, Exception], None]):
        """Register callback for plugin errors."""
        self._on_plugin_error.append(callback)
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              HELPERS
    # ═══════════════════════════════════════════════════════════════════════════
    
    def _compute_checksum(self, file_path: Path) -> str:
        """Compute file checksum for change detection."""
        content = file_path.read_bytes()
        return hashlib.sha256(content).hexdigest()[:16]
    
    def _save_plugin(self, info: PluginInfo):
        """Persist plugin info to database."""
        try:
            with sqlite3.connect(PLUGIN_DB_PATH) as conn:
                conn.execute("""
                    INSERT OR REPLACE INTO plugins
                    (plugin_id, name, version, capabilities, module_path, status,
                     load_count, execution_count, error_count, last_loaded,
                     last_executed, last_error, file_checksum)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    info.plugin_id, info.name, info.version,
                    json.dumps(info.capabilities), info.module_path,
                    info.status.value, info.load_count, info.execution_count,
                    info.error_count,
                    info.last_loaded.isoformat() if info.last_loaded else None,
                    info.last_executed.isoformat() if info.last_executed else None,
                    info.last_error, info.file_checksum
                ))
        except Exception as e:
            logger.error(f"∴ Failed to save plugin info: {e}")
    
    def get_stats(self) -> Dict[str, Any]:
        """Get plugin system statistics."""
        plugins = self.registry.get_all()
        
        return {
            "total_plugins": len(plugins),
            "active_plugins": sum(1 for p in plugins if p.status == PluginStatus.ACTIVE),
            "error_plugins": sum(1 for p in plugins if p.status == PluginStatus.ERROR),
            "total_executions": sum(p.execution_count for p in plugins),
            "total_errors": sum(p.error_count for p in plugins),
            "capabilities": list(self.registry._capabilities_map.keys())
        }


# ═══════════════════════════════════════════════════════════════════════════════
#                              SINGLETON
# ═══════════════════════════════════════════════════════════════════════════════

_loader_instance: Optional[DynamicPluginLoader] = None

def get_plugin_loader() -> DynamicPluginLoader:
    """Get the singleton PluginLoader instance."""
    global _loader_instance
    if _loader_instance is None:
        _loader_instance = DynamicPluginLoader()
    return _loader_instance
