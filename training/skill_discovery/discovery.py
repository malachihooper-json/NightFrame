"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME SKILL DISCOVERY MODULE                                ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Autonomously identifies new capabilities from data patterns.              ║
║  Uses state factorization and pattern clustering for skill discovery.     ║
╚═══════════════════════════════════════════════════════════════════════════╝
"""

import json
import hashlib
import logging
from datetime import datetime
from pathlib import Path
from dataclasses import dataclass, field, asdict
from typing import Dict, List, Optional, Any, Tuple
from enum import Enum
import sqlite3
import numpy as np

logger = logging.getLogger("NIGHTFRAME.SkillDiscovery")

# ═══════════════════════════════════════════════════════════════════════════════
#                              DATA CLASSES
# ═══════════════════════════════════════════════════════════════════════════════

class SkillStatus(Enum):
    CANDIDATE = "candidate"      # Newly discovered, not validated
    TESTING = "testing"          # Being validated
    ACTIVE = "active"            # Validated and in use
    DEPRECATED = "deprecated"    # No longer useful
    FAILED = "failed"            # Validation failed


@dataclass
class DiscoveredSkill:
    """A skill discovered from data patterns."""
    skill_id: str
    name: str
    description: str
    domain: str
    
    # Discovery metadata
    discovered_at: datetime = field(default_factory=datetime.utcnow)
    discovery_method: str = "pattern_clustering"
    source_data: str = ""
    
    # Pattern information
    pattern_signature: str = ""    # Hash of the pattern
    pattern_features: Dict[str, float] = field(default_factory=dict)
    
    # Performance
    utility_score: float = 0.0     # Estimated usefulness (0-1)
    confidence: float = 0.0        # Confidence in this skill (0-1)
    validation_count: int = 0
    success_count: int = 0
    
    # Implementation
    status: SkillStatus = SkillStatus.CANDIDATE
    model_path: Optional[str] = None
    code_module: Optional[str] = None
    dependencies: List[str] = field(default_factory=list)
    
    # Lineage
    parent_skill: Optional[str] = None  # If derived from another skill


@dataclass
class PatternCluster:
    """A cluster of similar patterns in the data."""
    cluster_id: str
    centroid: List[float]
    size: int
    variance: float
    domain: str
    samples: List[str] = field(default_factory=list)  # Sample IDs
    is_novel: bool = False


# ═══════════════════════════════════════════════════════════════════════════════
#                              SKILL DISCOVERY ENGINE
# ═══════════════════════════════════════════════════════════════════════════════

class SkillDiscovery:
    """
    Autonomous skill discovery from data patterns.
    
    Techniques:
    1. State Factorization - Decompose state into factors, find skills that 
       induce diverse interactions between factors
    2. Pattern Clustering - Group similar behaviors to identify skill categories
    3. Novelty Detection - Flag patterns not covered by existing skills
    4. Utility Scoring - Estimate usefulness of discovered skills
    """
    
    def __init__(self, db_path: Path = None):
        self.db_path = db_path or Path("models/skill_discovery.db")
        self._discovered_skills: Dict[str, DiscoveredSkill] = {}
        self._pattern_clusters: Dict[str, PatternCluster] = {}
        self._known_patterns: set = set()
        
        self._init_database()
        self._load_skills()
        
        logger.info("◈ SkillDiscovery initialized")
    
    def _init_database(self):
        """Initialize SQLite database."""
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        
        with sqlite3.connect(self.db_path) as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS discovered_skills (
                    skill_id TEXT PRIMARY KEY,
                    name TEXT,
                    description TEXT,
                    domain TEXT,
                    discovered_at TEXT,
                    discovery_method TEXT,
                    pattern_signature TEXT,
                    pattern_features TEXT,
                    utility_score REAL,
                    confidence REAL,
                    validation_count INTEGER,
                    success_count INTEGER,
                    status TEXT,
                    model_path TEXT,
                    code_module TEXT,
                    dependencies TEXT,
                    parent_skill TEXT
                );
                
                CREATE TABLE IF NOT EXISTS pattern_clusters (
                    cluster_id TEXT PRIMARY KEY,
                    centroid TEXT,
                    size INTEGER,
                    variance REAL,
                    domain TEXT,
                    is_novel INTEGER
                );
                
                CREATE INDEX IF NOT EXISTS idx_skills_domain 
                    ON discovered_skills(domain);
                CREATE INDEX IF NOT EXISTS idx_skills_status 
                    ON discovered_skills(status);
            """)
    
    def _load_skills(self):
        """Load discovered skills from database."""
        try:
            with sqlite3.connect(self.db_path) as conn:
                cursor = conn.execute("""
                    SELECT skill_id, name, description, domain, discovered_at,
                           discovery_method, pattern_signature, pattern_features,
                           utility_score, confidence, validation_count, success_count,
                           status, model_path, code_module, dependencies, parent_skill
                    FROM discovered_skills
                    WHERE status != 'deprecated'
                """)
                
                for row in cursor.fetchall():
                    skill = DiscoveredSkill(
                        skill_id=row[0],
                        name=row[1],
                        description=row[2],
                        domain=row[3],
                        discovered_at=datetime.fromisoformat(row[4]) if row[4] else datetime.utcnow(),
                        discovery_method=row[5] or "unknown",
                        pattern_signature=row[6] or "",
                        pattern_features=json.loads(row[7]) if row[7] else {},
                        utility_score=row[8] or 0.0,
                        confidence=row[9] or 0.0,
                        validation_count=row[10] or 0,
                        success_count=row[11] or 0,
                        status=SkillStatus(row[12]) if row[12] else SkillStatus.CANDIDATE,
                        model_path=row[13],
                        code_module=row[14],
                        dependencies=json.loads(row[15]) if row[15] else [],
                        parent_skill=row[16]
                    )
                    self._discovered_skills[skill.skill_id] = skill
                    self._known_patterns.add(skill.pattern_signature)
                
            logger.info(f"◎ Loaded {len(self._discovered_skills)} discovered skills")
        except Exception as e:
            logger.warning(f"∴ Could not load skills: {e}")
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              DISCOVERY PIPELINE
    # ═══════════════════════════════════════════════════════════════════════════
    
    def analyze_patterns(self, data: np.ndarray, domain: str, 
                         labels: Optional[np.ndarray] = None) -> List[PatternCluster]:
        """
        Extract latent patterns from training data using clustering.
        
        Args:
            data: Feature matrix (n_samples, n_features)
            domain: Capability domain this data relates to
            labels: Optional ground truth labels
            
        Returns:
            List of discovered pattern clusters
        """
        if len(data) < 10:
            logger.warning("Insufficient data for pattern analysis")
            return []
        
        # Normalize features
        data_norm = self._normalize(data)
        
        # Simple k-means clustering (can be enhanced with more sophisticated methods)
        clusters = self._cluster_patterns(data_norm, domain)
        
        # Check for novelty
        for cluster in clusters:
            signature = self._compute_pattern_signature(cluster.centroid)
            cluster.is_novel = signature not in self._known_patterns
        
        # Store clusters
        for cluster in clusters:
            self._pattern_clusters[cluster.cluster_id] = cluster
        
        novel_count = sum(1 for c in clusters if c.is_novel)
        logger.info(f"◎ Found {len(clusters)} patterns, {novel_count} novel")
        
        return clusters
    
    def identify_skill_candidates(self, clusters: Optional[List[PatternCluster]] = None) -> List[DiscoveredSkill]:
        """
        Propose new skills from pattern analysis.
        
        Each novel pattern cluster becomes a skill candidate.
        """
        if clusters is None:
            clusters = [c for c in self._pattern_clusters.values() if c.is_novel]
        
        candidates = []
        
        for cluster in clusters:
            if not cluster.is_novel:
                continue
            
            signature = self._compute_pattern_signature(cluster.centroid)
            
            # Check if we already have this skill
            if signature in self._known_patterns:
                continue
            
            # Create skill candidate
            skill = DiscoveredSkill(
                skill_id=self._generate_id("SKILL"),
                name=f"Auto_{cluster.domain}_{len(self._discovered_skills)}",
                description=f"Automatically discovered skill for {cluster.domain}",
                domain=cluster.domain,
                discovery_method="pattern_clustering",
                pattern_signature=signature,
                pattern_features={f"dim_{i}": float(v) for i, v in enumerate(cluster.centroid)},
                utility_score=self._estimate_utility(cluster),
                status=SkillStatus.CANDIDATE
            )
            
            candidates.append(skill)
            self._known_patterns.add(signature)
        
        logger.info(f"◎ Generated {len(candidates)} skill candidates")
        return candidates
    
    def evaluate_utility(self, skill: DiscoveredSkill) -> float:
        """
        Score potential usefulness of a discovered skill.
        
        Factors:
        - Pattern distinctiveness
        - Domain priority
        - Cluster size (more samples = more important)
        - Coverage gap (does it fill a gap?)
        """
        score = 0.5  # Base score
        
        # Domain priority bonus
        priority_domains = {
            "4g_lte": 0.2,
            "5g_nr": 0.2,
            "network_handover": 0.15,
            "wifi_ap": 0.15,
            "internet_provision": 0.2,
            "rf_fingerprinting": 0.1,
        }
        score += priority_domains.get(skill.domain, 0.05)
        
        # Feature distinctiveness
        if skill.pattern_features:
            variance = np.var(list(skill.pattern_features.values()))
            score += min(0.2, variance)  # Higher variance = more distinctive
        
        return min(1.0, score)
    
    def register_skill(self, skill: DiscoveredSkill) -> bool:
        """
        Add a verified skill to the manifest.
        """
        try:
            skill.status = SkillStatus.ACTIVE
            self._discovered_skills[skill.skill_id] = skill
            self._save_skill(skill)
            
            logger.info(f"◈ Registered skill: {skill.name}")
            return True
        except Exception as e:
            logger.error(f"∴ Failed to register skill: {e}")
            return False
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              QUERY METHODS
    # ═══════════════════════════════════════════════════════════════════════════
    
    def get_skills_by_domain(self, domain: str) -> List[DiscoveredSkill]:
        """Get all skills for a specific domain."""
        return [s for s in self._discovered_skills.values() if s.domain == domain]
    
    def get_active_skills(self) -> List[DiscoveredSkill]:
        """Get all active skills."""
        return [s for s in self._discovered_skills.values() if s.status == SkillStatus.ACTIVE]
    
    def get_skill(self, skill_id: str) -> Optional[DiscoveredSkill]:
        """Get a specific skill by ID."""
        return self._discovered_skills.get(skill_id)
    
    def get_candidate_skills(self) -> List[DiscoveredSkill]:
        """Get skills awaiting validation."""
        return [s for s in self._discovered_skills.values() if s.status == SkillStatus.CANDIDATE]
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              HELPER METHODS
    # ═══════════════════════════════════════════════════════════════════════════
    
    def _normalize(self, data: np.ndarray) -> np.ndarray:
        """Normalize data to zero mean, unit variance."""
        mean = np.mean(data, axis=0)
        std = np.std(data, axis=0) + 1e-8
        return (data - mean) / std
    
    def _cluster_patterns(self, data: np.ndarray, domain: str, 
                          n_clusters: int = 5) -> List[PatternCluster]:
        """Simple k-means clustering."""
        n_samples = len(data)
        n_clusters = min(n_clusters, n_samples // 2)
        
        if n_clusters < 2:
            return []
        
        # Random initialization
        indices = np.random.choice(n_samples, n_clusters, replace=False)
        centroids = data[indices].copy()
        
        # K-means iterations
        for _ in range(20):
            # Assign points to clusters
            distances = np.linalg.norm(data[:, np.newaxis] - centroids, axis=2)
            assignments = np.argmin(distances, axis=1)
            
            # Update centroids
            new_centroids = np.array([
                data[assignments == k].mean(axis=0) if np.sum(assignments == k) > 0 
                else centroids[k]
                for k in range(n_clusters)
            ])
            
            if np.allclose(centroids, new_centroids):
                break
            centroids = new_centroids
        
        # Create cluster objects
        clusters = []
        for k in range(n_clusters):
            mask = assignments == k
            cluster_data = data[mask]
            
            if len(cluster_data) == 0:
                continue
            
            cluster = PatternCluster(
                cluster_id=self._generate_id("CLU"),
                centroid=centroids[k].tolist(),
                size=len(cluster_data),
                variance=float(np.var(cluster_data)),
                domain=domain
            )
            clusters.append(cluster)
        
        return clusters
    
    def _compute_pattern_signature(self, centroid: List[float]) -> str:
        """Compute a hash signature for a pattern centroid."""
        # Quantize to reduce sensitivity
        quantized = [round(v, 2) for v in centroid]
        data = json.dumps(quantized).encode()
        return hashlib.sha256(data).hexdigest()[:16]
    
    def _estimate_utility(self, cluster: PatternCluster) -> float:
        """Estimate utility of a pattern cluster."""
        # Larger clusters are more important
        size_score = min(1.0, cluster.size / 100)
        
        # Lower variance means more coherent pattern
        variance_score = max(0, 1.0 - cluster.variance)
        
        # Novel patterns get a bonus
        novelty_bonus = 0.2 if cluster.is_novel else 0.0
        
        return (size_score * 0.4 + variance_score * 0.4 + novelty_bonus)
    
    def _save_skill(self, skill: DiscoveredSkill):
        """Persist a skill to the database."""
        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                INSERT OR REPLACE INTO discovered_skills 
                (skill_id, name, description, domain, discovered_at, discovery_method,
                 pattern_signature, pattern_features, utility_score, confidence,
                 validation_count, success_count, status, model_path, code_module,
                 dependencies, parent_skill)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                skill.skill_id,
                skill.name,
                skill.description,
                skill.domain,
                skill.discovered_at.isoformat(),
                skill.discovery_method,
                skill.pattern_signature,
                json.dumps(skill.pattern_features),
                skill.utility_score,
                skill.confidence,
                skill.validation_count,
                skill.success_count,
                skill.status.value,
                skill.model_path,
                skill.code_module,
                json.dumps(skill.dependencies),
                skill.parent_skill
            ))
    
    def _generate_id(self, prefix: str) -> str:
        """Generate a unique ID."""
        timestamp = datetime.utcnow().strftime("%Y%m%d%H%M%S%f")
        hash_part = hashlib.md5(f"{prefix}{timestamp}".encode()).hexdigest()[:8]
        return f"{prefix}_{timestamp}_{hash_part}"


# ═══════════════════════════════════════════════════════════════════════════════
#                              SINGLETON
# ═══════════════════════════════════════════════════════════════════════════════

_discovery_instance: Optional[SkillDiscovery] = None

def get_skill_discovery() -> SkillDiscovery:
    """Get the singleton SkillDiscovery instance."""
    global _discovery_instance
    if _discovery_instance is None:
        _discovery_instance = SkillDiscovery()
    return _discovery_instance
