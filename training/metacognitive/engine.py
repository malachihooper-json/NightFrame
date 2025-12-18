"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME METACOGNITIVE ENGINE                                  ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Central reasoning system for self-reflection, performance monitoring,    ║
║  and autonomous learning adaptation. Fully autonomous operation.          ║
╚═══════════════════════════════════════════════════════════════════════════╝

The Metacognitive Engine is the "brain of the brain" - it monitors the AI's
own learning processes, identifies gaps, and orchestrates self-improvement.
"""

import json
import logging
import hashlib
from datetime import datetime
from pathlib import Path
from dataclasses import dataclass, field, asdict
from typing import Dict, List, Optional, Any, Callable
from enum import Enum
import threading
import sqlite3

# ═══════════════════════════════════════════════════════════════════════════════
#                              CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

METACOG_DB_PATH = Path(__file__).parent.parent.parent / "models" / "metacognitive.db"
STATE_SNAPSHOT_PATH = Path(__file__).parent.parent.parent / "models" / "metacog_state.json"

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("NIGHTFRAME.Metacognitive")


# ═══════════════════════════════════════════════════════════════════════════════
#                              ENUMS & DATA CLASSES
# ═══════════════════════════════════════════════════════════════════════════════

class CapabilityDomain(Enum):
    """Priority capability domains for NIGHTFRAME."""
    NETWORK_4G_LTE = "4g_lte"
    NETWORK_5G_NR = "5g_nr"
    NETWORK_HANDOVER = "network_handover"
    WIFI_ACCESS_POINT = "wifi_ap"
    CELLULAR_INTELLIGENCE = "cellular_intelligence"
    RF_FINGERPRINTING = "rf_fingerprinting"
    LOCATION_PREDICTION = "location_prediction"
    SWARM_COORDINATION = "swarm_coordination"
    INTERNET_PROVISION = "internet_provision"
    MESH_ROUTING = "mesh_routing"


class LearningPhase(Enum):
    """Current phase of the metacognitive learning cycle."""
    REFLECTING = "reflecting"       # Analyzing current state
    PLANNING = "planning"           # Determining next objectives
    EXECUTING = "executing"         # Running training/adaptation
    EVALUATING = "evaluating"       # Scoring outcomes
    ADAPTING = "adapting"           # Modifying strategies


class ConfidenceLevel(Enum):
    """Confidence in a capability."""
    UNKNOWN = 0
    LOW = 1
    MODERATE = 2
    HIGH = 3
    VERIFIED = 4


@dataclass
class PerformanceMetric:
    """A single performance measurement."""
    metric_id: str
    capability_domain: str
    metric_name: str
    value: float
    timestamp: datetime = field(default_factory=datetime.utcnow)
    context: Dict[str, Any] = field(default_factory=dict)


@dataclass
class LearningGap:
    """Identified gap in current capabilities."""
    gap_id: str
    domain: CapabilityDomain
    description: str
    severity: float  # 0-1, higher = more critical
    discovered_at: datetime = field(default_factory=datetime.utcnow)
    resolution_attempts: int = 0
    is_resolved: bool = False


@dataclass
class LearningObjective:
    """A planned learning objective."""
    objective_id: str
    domain: CapabilityDomain
    description: str
    target_metric: str
    target_value: float
    priority: int  # 1-10, higher = more important
    created_at: datetime = field(default_factory=datetime.utcnow)
    deadline: Optional[datetime] = None
    is_achieved: bool = False


@dataclass
class AdaptationStrategy:
    """A strategy for adapting the learning process."""
    strategy_id: str
    name: str
    description: str
    parameters: Dict[str, Any]
    success_rate: float = 0.0
    usage_count: int = 0
    is_active: bool = True


@dataclass
class MetacognitiveState:
    """Complete state of the metacognitive engine."""
    current_phase: LearningPhase
    capabilities: Dict[str, float]  # domain -> confidence score
    active_objectives: List[LearningObjective]
    identified_gaps: List[LearningGap]
    adaptation_strategies: List[AdaptationStrategy]
    performance_history: List[PerformanceMetric]
    total_learning_cycles: int
    total_skills_discovered: int
    total_adaptations: int
    last_reflection: Optional[datetime]
    last_adaptation: Optional[datetime]


# ═══════════════════════════════════════════════════════════════════════════════
#                              METACOGNITIVE ENGINE
# ═══════════════════════════════════════════════════════════════════════════════

class MetacognitiveEngine:
    """
    The Metacognitive Engine monitors and improves the AI's learning processes.
    
    Core responsibilities:
    1. REFLECT - Analyze current model performance and identify gaps
    2. PLAN - Determine next learning objectives based on gaps
    3. EVALUATE - Score achieved outcomes against planned objectives
    4. ADAPT - Modify learning strategies based on evaluation
    
    Operates fully autonomously without human intervention.
    """
    
    def __init__(self, db_path: Path = METACOG_DB_PATH):
        self.db_path = db_path
        self._lock = threading.RLock()
        
        # Core state
        self._phase = LearningPhase.REFLECTING
        self._capabilities: Dict[str, float] = {}
        self._objectives: List[LearningObjective] = []
        self._gaps: List[LearningGap] = []
        self._strategies: List[AdaptationStrategy] = []
        self._metrics: List[PerformanceMetric] = []
        
        # Counters
        self._learning_cycles = 0
        self._skills_discovered = 0
        self._adaptations = 0
        
        # Timestamps
        self._last_reflection: Optional[datetime] = None
        self._last_adaptation: Optional[datetime] = None
        
        # Callbacks for external integration
        self._on_gap_identified: List[Callable[[LearningGap], None]] = []
        self._on_skill_discovered: List[Callable[[str, Dict], None]] = []
        self._on_adaptation: List[Callable[[AdaptationStrategy], None]] = []
        
        # Initialize
        self._init_database()
        self._init_default_capabilities()
        self._load_state()
        
        logger.info("◈ MetacognitiveEngine initialized")
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              INITIALIZATION
    # ═══════════════════════════════════════════════════════════════════════════
    
    def _init_database(self):
        """Initialize SQLite database for persistence."""
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        
        with sqlite3.connect(self.db_path) as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS capabilities (
                    domain TEXT PRIMARY KEY,
                    confidence REAL,
                    last_updated TEXT
                );
                
                CREATE TABLE IF NOT EXISTS performance_metrics (
                    metric_id TEXT PRIMARY KEY,
                    capability_domain TEXT,
                    metric_name TEXT,
                    value REAL,
                    timestamp TEXT,
                    context TEXT
                );
                
                CREATE TABLE IF NOT EXISTS learning_gaps (
                    gap_id TEXT PRIMARY KEY,
                    domain TEXT,
                    description TEXT,
                    severity REAL,
                    discovered_at TEXT,
                    resolution_attempts INTEGER,
                    is_resolved INTEGER
                );
                
                CREATE TABLE IF NOT EXISTS objectives (
                    objective_id TEXT PRIMARY KEY,
                    domain TEXT,
                    description TEXT,
                    target_metric TEXT,
                    target_value REAL,
                    priority INTEGER,
                    created_at TEXT,
                    deadline TEXT,
                    is_achieved INTEGER
                );
                
                CREATE TABLE IF NOT EXISTS adaptation_strategies (
                    strategy_id TEXT PRIMARY KEY,
                    name TEXT,
                    description TEXT,
                    parameters TEXT,
                    success_rate REAL,
                    usage_count INTEGER,
                    is_active INTEGER
                );
                
                CREATE TABLE IF NOT EXISTS state_snapshots (
                    snapshot_id TEXT PRIMARY KEY,
                    timestamp TEXT,
                    state_json TEXT
                );
                
                CREATE INDEX IF NOT EXISTS idx_metrics_domain 
                    ON performance_metrics(capability_domain);
                CREATE INDEX IF NOT EXISTS idx_metrics_timestamp 
                    ON performance_metrics(timestamp);
            """)
    
    def _init_default_capabilities(self):
        """Initialize default capability domains with zero confidence."""
        for domain in CapabilityDomain:
            if domain.value not in self._capabilities:
                self._capabilities[domain.value] = 0.0
    
    def _load_state(self):
        """Load persisted state from database."""
        try:
            with sqlite3.connect(self.db_path) as conn:
                # Load capabilities
                cursor = conn.execute("SELECT domain, confidence FROM capabilities")
                for domain, confidence in cursor.fetchall():
                    self._capabilities[domain] = confidence
                
                # Load recent metrics (last 1000)
                cursor = conn.execute("""
                    SELECT metric_id, capability_domain, metric_name, value, timestamp, context 
                    FROM performance_metrics 
                    ORDER BY timestamp DESC LIMIT 1000
                """)
                for row in cursor.fetchall():
                    self._metrics.append(PerformanceMetric(
                        metric_id=row[0],
                        capability_domain=row[1],
                        metric_name=row[2],
                        value=row[3],
                        timestamp=datetime.fromisoformat(row[4]),
                        context=json.loads(row[5]) if row[5] else {}
                    ))
                
            logger.info(f"◎ Loaded {len(self._capabilities)} capabilities, {len(self._metrics)} metrics")
        except Exception as e:
            logger.warning(f"∴ Could not load state: {e}")
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              CORE METACOGNITIVE LOOP
    # ═══════════════════════════════════════════════════════════════════════════
    
    def run_cycle(self, training_data: Optional[Any] = None) -> Dict[str, Any]:
        """
        Execute one complete metacognitive cycle.
        
        Returns:
            Dict with cycle results including identified gaps, objectives, and adaptations.
        """
        with self._lock:
            cycle_start = datetime.utcnow()
            self._learning_cycles += 1
            
            logger.info(f"◈ Starting metacognitive cycle #{self._learning_cycles}")
            
            results = {
                "cycle_id": self._learning_cycles,
                "timestamp": cycle_start.isoformat(),
                "gaps_identified": [],
                "objectives_created": [],
                "adaptations_made": [],
                "skills_discovered": []
            }
            
            try:
                # Phase 1: REFLECT
                self._phase = LearningPhase.REFLECTING
                gaps = self._reflect()
                results["gaps_identified"] = [asdict(g) for g in gaps]
                
                # Phase 2: PLAN
                self._phase = LearningPhase.PLANNING
                objectives = self._plan(gaps)
                results["objectives_created"] = [asdict(o) for o in objectives]
                
                # Phase 3: EVALUATE (if we have training data)
                self._phase = LearningPhase.EVALUATING
                if training_data is not None:
                    evaluation = self._evaluate(training_data)
                    results["evaluation"] = evaluation
                
                # Phase 4: ADAPT
                self._phase = LearningPhase.ADAPTING
                adaptations = self._adapt()
                results["adaptations_made"] = [asdict(a) for a in adaptations]
                
                # Persist state
                self._save_state()
                
            except Exception as e:
                logger.error(f"∴ Cycle error: {e}")
                results["error"] = str(e)
            
            cycle_duration = (datetime.utcnow() - cycle_start).total_seconds()
            results["duration_seconds"] = cycle_duration
            
            logger.info(f"◈ Cycle #{self._learning_cycles} complete in {cycle_duration:.2f}s")
            
            return results
    
    def _reflect(self) -> List[LearningGap]:
        """
        REFLECT phase: Analyze current state and identify gaps.
        
        Examines:
        - Current capability confidence scores
        - Recent performance metrics
        - Unmet objectives
        """
        self._last_reflection = datetime.utcnow()
        identified_gaps = []
        
        # Check each priority domain
        priority_domains = [
            (CapabilityDomain.NETWORK_4G_LTE, 0.9),    # Target 90% confidence
            (CapabilityDomain.NETWORK_5G_NR, 0.8),
            (CapabilityDomain.NETWORK_HANDOVER, 0.85),
            (CapabilityDomain.WIFI_ACCESS_POINT, 0.9),
            (CapabilityDomain.INTERNET_PROVISION, 0.95),
            (CapabilityDomain.RF_FINGERPRINTING, 0.75),
            (CapabilityDomain.LOCATION_PREDICTION, 0.7),
            (CapabilityDomain.MESH_ROUTING, 0.8),
        ]
        
        for domain, target in priority_domains:
            current = self._capabilities.get(domain.value, 0.0)
            
            if current < target:
                severity = (target - current) / target  # Relative gap
                
                gap = LearningGap(
                    gap_id=self._generate_id("GAP"),
                    domain=domain,
                    description=f"Capability {domain.value} at {current:.1%}, target {target:.1%}",
                    severity=severity
                )
                
                identified_gaps.append(gap)
                self._gaps.append(gap)
                
                # Notify callbacks
                for callback in self._on_gap_identified:
                    try:
                        callback(gap)
                    except Exception as e:
                        logger.error(f"Gap callback error: {e}")
        
        logger.info(f"◎ Reflection identified {len(identified_gaps)} gaps")
        return identified_gaps
    
    def _plan(self, gaps: List[LearningGap]) -> List[LearningObjective]:
        """
        PLAN phase: Create learning objectives to address gaps.
        """
        objectives = []
        
        # Sort gaps by severity
        sorted_gaps = sorted(gaps, key=lambda g: g.severity, reverse=True)
        
        for gap in sorted_gaps[:5]:  # Focus on top 5 gaps
            objective = LearningObjective(
                objective_id=self._generate_id("OBJ"),
                domain=gap.domain,
                description=f"Improve {gap.domain.value} capability",
                target_metric=f"{gap.domain.value}_accuracy",
                target_value=self._capabilities.get(gap.domain.value, 0) + 0.1,  # +10%
                priority=int(gap.severity * 10)
            )
            
            objectives.append(objective)
            self._objectives.append(objective)
        
        logger.info(f"◎ Planned {len(objectives)} objectives")
        return objectives
    
    def _evaluate(self, training_data: Any) -> Dict[str, Any]:
        """
        EVALUATE phase: Score outcomes against objectives.
        """
        evaluation = {
            "total_objectives": len(self._objectives),
            "achieved": 0,
            "in_progress": 0,
            "metrics": {}
        }
        
        # Evaluate each active objective
        for obj in self._objectives:
            if obj.is_achieved:
                continue
            
            current = self._capabilities.get(obj.domain.value, 0)
            if current >= obj.target_value:
                obj.is_achieved = True
                evaluation["achieved"] += 1
            else:
                evaluation["in_progress"] += 1
        
        return evaluation
    
    def _adapt(self) -> List[AdaptationStrategy]:
        """
        ADAPT phase: Modify learning strategies based on performance.
        """
        self._last_adaptation = datetime.utcnow()
        adaptations = []
        
        # Analyze which strategies are working
        successful_strategies = [s for s in self._strategies if s.success_rate > 0.7]
        failing_strategies = [s for s in self._strategies if s.success_rate < 0.3 and s.usage_count > 5]
        
        # Disable failing strategies
        for strategy in failing_strategies:
            strategy.is_active = False
            logger.info(f"∴ Disabled strategy: {strategy.name}")
        
        # Create new strategies for unaddressed gaps
        unresolved_gaps = [g for g in self._gaps if not g.is_resolved]
        
        for gap in unresolved_gaps[:3]:
            if gap.resolution_attempts < 3:
                # Generate a new adaptation strategy
                strategy = AdaptationStrategy(
                    strategy_id=self._generate_id("STR"),
                    name=f"Adaptive_{gap.domain.value}",
                    description=f"Auto-generated strategy for {gap.domain.value}",
                    parameters={
                        "learning_rate_multiplier": 1.0 + (gap.resolution_attempts * 0.5),
                        "batch_size_multiplier": 1.0 + (gap.resolution_attempts * 0.25),
                        "epochs_multiplier": 1.0 + (gap.resolution_attempts * 0.5),
                        "target_domain": gap.domain.value
                    }
                )
                
                adaptations.append(strategy)
                self._strategies.append(strategy)
                gap.resolution_attempts += 1
                self._adaptations += 1
                
                # Notify callbacks
                for callback in self._on_adaptation:
                    try:
                        callback(strategy)
                    except Exception as e:
                        logger.error(f"Adaptation callback error: {e}")
        
        logger.info(f"◎ Created {len(adaptations)} adaptations")
        return adaptations
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              CAPABILITY MANAGEMENT
    # ═══════════════════════════════════════════════════════════════════════════
    
    def update_capability(self, domain: str, confidence: float, source: str = "training"):
        """Update confidence score for a capability domain."""
        with self._lock:
            old_value = self._capabilities.get(domain, 0)
            self._capabilities[domain] = max(0, min(1, confidence))  # Clamp 0-1
            
            # Record metric
            metric = PerformanceMetric(
                metric_id=self._generate_id("MET"),
                capability_domain=domain,
                metric_name="confidence",
                value=confidence,
                context={"source": source, "previous": old_value}
            )
            self._metrics.append(metric)
            
            # Check if skill threshold crossed
            if old_value < 0.5 and confidence >= 0.5:
                self._skills_discovered += 1
                logger.info(f"◈ Skill discovered: {domain} at {confidence:.1%}")
                
                for callback in self._on_skill_discovered:
                    try:
                        callback(domain, {"confidence": confidence})
                    except Exception as e:
                        logger.error(f"Skill callback error: {e}")
    
    def get_capability(self, domain: str) -> float:
        """Get current confidence for a capability domain."""
        return self._capabilities.get(domain, 0.0)
    
    def get_all_capabilities(self) -> Dict[str, float]:
        """Get all capability confidence scores."""
        return dict(self._capabilities)
    
    def get_active_objectives(self) -> List[LearningObjective]:
        """Get list of active learning objectives."""
        return [o for o in self._objectives if not o.is_achieved]
    
    def get_unresolved_gaps(self) -> List[LearningGap]:
        """Get list of unresolved learning gaps."""
        return [g for g in self._gaps if not g.is_resolved]
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              CALLBACKS
    # ═══════════════════════════════════════════════════════════════════════════
    
    def on_gap_identified(self, callback: Callable[[LearningGap], None]):
        """Register callback for when a gap is identified."""
        self._on_gap_identified.append(callback)
    
    def on_skill_discovered(self, callback: Callable[[str, Dict], None]):
        """Register callback for when a skill is discovered."""
        self._on_skill_discovered.append(callback)
    
    def on_adaptation(self, callback: Callable[[AdaptationStrategy], None]):
        """Register callback for when an adaptation is made."""
        self._on_adaptation.append(callback)
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              PERSISTENCE
    # ═══════════════════════════════════════════════════════════════════════════
    
    def _save_state(self):
        """Persist current state to database."""
        try:
            with sqlite3.connect(self.db_path) as conn:
                # Save capabilities
                for domain, confidence in self._capabilities.items():
                    conn.execute("""
                        INSERT OR REPLACE INTO capabilities (domain, confidence, last_updated)
                        VALUES (?, ?, ?)
                    """, (domain, confidence, datetime.utcnow().isoformat()))
                
                # Save recent metrics
                for metric in self._metrics[-100:]:  # Last 100
                    conn.execute("""
                        INSERT OR IGNORE INTO performance_metrics 
                        (metric_id, capability_domain, metric_name, value, timestamp, context)
                        VALUES (?, ?, ?, ?, ?, ?)
                    """, (
                        metric.metric_id,
                        metric.capability_domain,
                        metric.metric_name,
                        metric.value,
                        metric.timestamp.isoformat(),
                        json.dumps(metric.context)
                    ))
                
                conn.commit()
                
        except Exception as e:
            logger.error(f"∴ State save error: {e}")
    
    def get_state(self) -> MetacognitiveState:
        """Get complete current state."""
        return MetacognitiveState(
            current_phase=self._phase,
            capabilities=dict(self._capabilities),
            active_objectives=self.get_active_objectives(),
            identified_gaps=self.get_unresolved_gaps(),
            adaptation_strategies=[s for s in self._strategies if s.is_active],
            performance_history=self._metrics[-100:],
            total_learning_cycles=self._learning_cycles,
            total_skills_discovered=self._skills_discovered,
            total_adaptations=self._adaptations,
            last_reflection=self._last_reflection,
            last_adaptation=self._last_adaptation
        )
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              UTILITIES
    # ═══════════════════════════════════════════════════════════════════════════
    
    def _generate_id(self, prefix: str) -> str:
        """Generate a unique ID."""
        timestamp = datetime.utcnow().strftime("%Y%m%d%H%M%S%f")
        hash_part = hashlib.md5(f"{prefix}{timestamp}".encode()).hexdigest()[:8]
        return f"{prefix}_{timestamp}_{hash_part}"
    
    def __repr__(self):
        return (
            f"MetacognitiveEngine("
            f"phase={self._phase.value}, "
            f"capabilities={len(self._capabilities)}, "
            f"cycles={self._learning_cycles})"
        )


# ═══════════════════════════════════════════════════════════════════════════════
#                              SINGLETON INSTANCE
# ═══════════════════════════════════════════════════════════════════════════════

_metacog_instance: Optional[MetacognitiveEngine] = None

def get_metacognitive_engine() -> MetacognitiveEngine:
    """Get the singleton MetacognitiveEngine instance."""
    global _metacog_instance
    if _metacog_instance is None:
        _metacog_instance = MetacognitiveEngine()
    return _metacog_instance


if __name__ == "__main__":
    # Demo
    engine = get_metacognitive_engine()
    print(engine)
    
    # Run a cycle
    result = engine.run_cycle()
    print(f"Cycle result: {json.dumps(result, indent=2, default=str)}")
