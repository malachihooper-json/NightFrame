#!/usr/bin/env python3
"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME METACOGNITIVE TRAINING SYSTEM                         ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Main entry point for the fully autonomous training system.                ║
║  Integrates all components: Metacognition, Skill Discovery,                ║
║  Code Generation, Plugin Loading, and Ensemble Training.                   ║
╚═══════════════════════════════════════════════════════════════════════════╝

Priority Domains:
- 4G LTE / 5G NR network access
- Permissionless network movement
- WiFi access point provision
- Global free internet connectivity
"""

import argparse
import json
import logging
import sys
import time
from pathlib import Path
from datetime import datetime
from typing import Optional, Dict, Any

import numpy as np

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s | %(name)s | %(levelname)s | %(message)s'
)
logger = logging.getLogger("NIGHTFRAME.Train")

# ═══════════════════════════════════════════════════════════════════════════════
#                              IMPORTS
# ═══════════════════════════════════════════════════════════════════════════════

try:
    import torch
    TORCH_AVAILABLE = True
except ImportError:
    TORCH_AVAILABLE = False
    logger.warning("PyTorch not available - some features disabled")

# Local imports
from metacognitive import get_metacognitive_engine, CapabilityDomain
from skill_discovery import get_skill_discovery, DiscoveredSkill, SkillStatus
from codegen import get_code_generator
from plugins import get_plugin_loader

if TORCH_AVAILABLE:
    from ensemble import get_training_orchestrator


# ═══════════════════════════════════════════════════════════════════════════════
#                              METACOGNITIVE TRAINER
# ═══════════════════════════════════════════════════════════════════════════════

class MetacognitiveTrainer:
    """
    Orchestrates the complete metacognitive training pipeline.
    
    This is the main class that ties together:
    1. MetacognitiveEngine - Self-reflection and adaptation
    2. SkillDiscovery - Finding new capabilities in data
    3. TrainingOrchestrator - Training ensemble models
    4. CodeGenerator - Creating executable skills
    5. PluginLoader - Hot-loading new behaviors
    """
    
    def __init__(self, autonomous: bool = True):
        self.autonomous = autonomous
        
        # Initialize all components
        logger.info("◈ Initializing NIGHTFRAME Metacognitive Training System")
        
        self.metacog = get_metacognitive_engine()
        self.skill_discovery = get_skill_discovery()
        self.code_generator = get_code_generator()
        self.plugin_loader = get_plugin_loader()
        
        if TORCH_AVAILABLE:
            self.training_orchestrator = get_training_orchestrator()
        else:
            self.training_orchestrator = None
        
        # Wire up callbacks for autonomous operation
        self._setup_callbacks()
        
        # Stats
        self.cycles_completed = 0
        self.skills_generated = 0
        self.plugins_deployed = 0
        
        logger.info("◈ System initialized - Ready for autonomous operation")
    
    def _setup_callbacks(self):
        """Wire up callbacks between components."""
        
        # When a gap is identified, trigger skill discovery
        self.metacog.on_gap_identified(self._on_gap_identified)
        
        # When a skill is discovered, generate code
        self.metacog.on_skill_discovered(self._on_skill_discovered)
        
        # When an adaptation is made, retrain relevant models
        self.metacog.on_adaptation(self._on_adaptation)
        
        # When a plugin is loaded, update capabilities
        self.plugin_loader.on_plugin_loaded(self._on_plugin_loaded)
    
    def _on_gap_identified(self, gap):
        """Handle identified learning gap."""
        logger.info(f"◎ Gap identified: {gap.domain.value} (severity: {gap.severity:.2f})")
        
        # In autonomous mode, immediately try to address
        if self.autonomous:
            # Check if we have training data for this domain
            pass  # Will be addressed in next cycle
    
    def _on_skill_discovered(self, domain: str, metadata: Dict):
        """Handle newly discovered skill."""
        logger.info(f"◈ Skill discovered: {domain}")
        
        # Create skill object
        skill = DiscoveredSkill(
            skill_id=f"SKILL_{datetime.utcnow().strftime('%Y%m%d%H%M%S')}",
            name=f"Auto_{domain}",
            description=f"Auto-discovered skill for {domain}",
            domain=domain,
            pattern_features={"centroid": [0.0] * 10},
            utility_score=metadata.get("confidence", 0.5),
            status=SkillStatus.CANDIDATE
        )
        
        # In autonomous mode, immediately generate code
        if self.autonomous:
            self._generate_and_deploy_skill(skill)
    
    def _on_adaptation(self, strategy):
        """Handle learning adaptation."""
        logger.info(f"◎ Adaptation: {strategy.name}")
        
        # Could trigger retraining with new parameters
        if self.autonomous and self.training_orchestrator:
            # Adjust training config based on strategy
            config = self.training_orchestrator.config
            params = strategy.parameters
            
            if "learning_rate_multiplier" in params:
                config.learning_rate *= params["learning_rate_multiplier"]
    
    def _on_plugin_loaded(self, plugin_info):
        """Handle plugin load event."""
        logger.info(f"◈ Plugin loaded: {plugin_info.name}")
        self.plugins_deployed += 1
        
        # Update metacognitive capabilities
        for cap in plugin_info.capabilities:
            current = self.metacog.get_capability(cap)
            self.metacog.update_capability(cap, min(1.0, current + 0.1), "plugin_loaded")
    
    def _generate_and_deploy_skill(self, skill: DiscoveredSkill):
        """Generate code for a skill and deploy it."""
        try:
            # Generate code
            code = self.code_generator.generate(skill)
            
            # Validate
            if self.code_generator.validate(code):
                # Deploy
                if self.code_generator.deploy(code):
                    self.skills_generated += 1
                    logger.info(f"◈ Skill deployed: {skill.name}")
                    
                    # Load as plugin
                    if code.file_path:
                        self.plugin_loader.load(code.file_path)
                else:
                    logger.warning(f"∴ Deployment failed: {skill.name}")
            else:
                logger.warning(f"∴ Validation failed: {code.validation_errors}")
                
        except Exception as e:
            logger.error(f"∴ Skill generation error: {e}")
    
    # ═══════════════════════════════════════════════════════════════════════════
    #                              MAIN TRAINING LOOP
    # ═══════════════════════════════════════════════════════════════════════════
    
    def run_training_cycle(self, data: Optional[np.ndarray] = None) -> Dict[str, Any]:
        """
        Run one complete training cycle.
        
        1. Metacognitive reflection
        2. Skill discovery from data
        3. Model training
        4. Code generation
        5. Plugin deployment
        """
        cycle_start = datetime.utcnow()
        self.cycles_completed += 1
        
        logger.info(f"═══════════════════════════════════════════════")
        logger.info(f"◈ TRAINING CYCLE #{self.cycles_completed}")
        logger.info(f"═══════════════════════════════════════════════")
        
        results = {
            "cycle": self.cycles_completed,
            "timestamp": cycle_start.isoformat(),
            "phases": {}
        }
        
        # Phase 1: Metacognitive reflection
        logger.info("◎ Phase 1: Metacognitive Reflection")
        metacog_result = self.metacog.run_cycle()
        results["phases"]["metacognition"] = {
            "gaps": len(metacog_result.get("gaps_identified", [])),
            "objectives": len(metacog_result.get("objectives_created", [])),
            "adaptations": len(metacog_result.get("adaptations_made", []))
        }
        
        # Phase 2: Skill discovery
        if data is not None:
            logger.info("◎ Phase 2: Skill Discovery")
            
            # Reshape data for clustering: (samples, channels, time) -> (samples, features)
            # Flatten the last two dimensions for pattern analysis
            if len(data.shape) == 3:
                discovery_data = data.reshape(data.shape[0], -1)  # (samples, channels*time)
            else:
                discovery_data = data
            
            # Analyze patterns for each priority domain
            domains = ["4g_lte", "5g_nr", "network_handover", "wifi_ap"]
            
            total_skills = 0
            for domain in domains:
                clusters = self.skill_discovery.analyze_patterns(discovery_data, domain)
                candidates = self.skill_discovery.identify_skill_candidates(clusters)
                
                for skill in candidates:
                    skill.utility_score = self.skill_discovery.evaluate_utility(skill)
                    
                    if skill.utility_score > 0.5:
                        self.skill_discovery.register_skill(skill)
                        
                        if self.autonomous:
                            self._generate_and_deploy_skill(skill)
                        
                        total_skills += 1
            
            results["phases"]["skill_discovery"] = {
                "skills_discovered": total_skills
            }
        
        # Phase 3: Ensemble training
        if self.training_orchestrator and TORCH_AVAILABLE and data is not None:
            logger.info("◎ Phase 3: Ensemble Training")
            
            # Prepare training data
            train_data = torch.from_numpy(data).float()
            
            # Synthetic labels for demo (in production, use real data)
            train_labels = torch.randn(len(train_data), 2)  # lat, lon
            
            # Train location model
            training_result = self.training_orchestrator.train_model(
                "rf_location",
                train_data,
                train_labels,
                domain="cellular"
            )
            
            results["phases"]["training"] = {
                "model": training_result.model_name,
                "loss": training_result.final_loss,
                "epochs": training_result.epochs_completed
            }
            
            # Update capability based on training result
            accuracy = 1.0 - training_result.final_loss
            self.metacog.update_capability("rf_fingerprinting", accuracy, "training")
        
        # Phase 4: Plugin discovery
        logger.info("◎ Phase 4: Plugin Discovery")
        plugin_results = self.plugin_loader.load_all()
        results["phases"]["plugins"] = {
            "loaded": sum(1 for v in plugin_results.values() if v),
            "total": len(plugin_results)
        }
        
        # Summary
        cycle_duration = (datetime.utcnow() - cycle_start).total_seconds()
        results["duration_seconds"] = cycle_duration
        
        logger.info(f"═══════════════════════════════════════════════")
        logger.info(f"◈ CYCLE #{self.cycles_completed} COMPLETE ({cycle_duration:.1f}s)")
        logger.info(f"  Skills: {self.skills_generated} | Plugins: {self.plugins_deployed}")
        logger.info(f"═══════════════════════════════════════════════")
        
        return results
    
    def run_continuous(self, interval_seconds: int = 60, max_cycles: int = None):
        """
        Run continuous training loop.
        
        Args:
            interval_seconds: Seconds between cycles
            max_cycles: Maximum cycles (None for infinite)
        """
        logger.info(f"◈ Starting continuous training (interval={interval_seconds}s)")
        
        cycle_count = 0
        
        while max_cycles is None or cycle_count < max_cycles:
            try:
                # Generate synthetic data for demo
                # In production, this would come from real measurements
                data = self._generate_synthetic_data()
                
                # Run cycle
                self.run_training_cycle(data)
                cycle_count += 1
                
                # Wait for next cycle
                if max_cycles is None or cycle_count < max_cycles:
                    logger.info(f"◎ Waiting {interval_seconds}s until next cycle...")
                    time.sleep(interval_seconds)
                    
            except KeyboardInterrupt:
                logger.info("◎ Training interrupted by user")
                break
            except Exception as e:
                logger.error(f"∴ Cycle error: {e}")
                time.sleep(10)  # Brief pause before retry
        
        logger.info(f"◈ Training complete. {cycle_count} cycles executed.")
    
    def _generate_synthetic_data(self, n_samples: int = 1000) -> np.ndarray:
        """Generate synthetic RF measurement data for testing."""
        # Simulate RSSI/CSI measurements
        # Shape: (samples, 2, 100) - I/Q channels, 100 time steps
        data = np.random.randn(n_samples, 2, 100).astype(np.float32)
        
        # Add some structure (simulate cell tower patterns)
        for i in range(n_samples):
            # Add sinusoidal patterns (signal characteristics)
            freq = np.random.uniform(0.1, 0.5)
            phase = np.random.uniform(0, 2 * np.pi)
            t = np.linspace(0, 10, 100)
            
            data[i, 0] += np.sin(2 * np.pi * freq * t + phase) * 0.5
            data[i, 1] += np.cos(2 * np.pi * freq * t + phase) * 0.5
        
        return data
    
    def get_status(self) -> Dict[str, Any]:
        """Get current system status."""
        return {
            "autonomous": self.autonomous,
            "cycles_completed": self.cycles_completed,
            "skills_generated": self.skills_generated,
            "plugins_deployed": self.plugins_deployed,
            "capabilities": self.metacog.get_all_capabilities(),
            "active_plugins": len(self.plugin_loader.registry.get_active_plugins()),
            "training_stats": self.training_orchestrator.get_training_stats() if self.training_orchestrator else None
        }


# ═══════════════════════════════════════════════════════════════════════════════
#                              CLI
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(
        description="NIGHTFRAME Metacognitive Training System"
    )
    
    parser.add_argument(
        "--mode", 
        choices=["single", "continuous", "status"],
        default="single",
        help="Training mode"
    )
    
    parser.add_argument(
        "--interval",
        type=int,
        default=60,
        help="Seconds between continuous training cycles"
    )
    
    parser.add_argument(
        "--cycles",
        type=int,
        default=None,
        help="Maximum training cycles (continuous mode)"
    )
    
    parser.add_argument(
        "--no-autonomous",
        action="store_true",
        help="Disable autonomous skill generation"
    )
    
    args = parser.parse_args()
    
    # Banner
    print("""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME METACOGNITIVE TRAINING SYSTEM v2.0                    ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Fully Autonomous | Self-Improving | Skill Discovery | Code Generation    ║
╚═══════════════════════════════════════════════════════════════════════════╝
    """)
    
    # Initialize trainer
    trainer = MetacognitiveTrainer(autonomous=not args.no_autonomous)
    
    if args.mode == "status":
        status = trainer.get_status()
        print(json.dumps(status, indent=2, default=str))
        
    elif args.mode == "single":
        data = trainer._generate_synthetic_data()
        result = trainer.run_training_cycle(data)
        print(f"\nResults: {json.dumps(result, indent=2, default=str)}")
        
    elif args.mode == "continuous":
        trainer.run_continuous(
            interval_seconds=args.interval,
            max_cycles=args.cycles
        )
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
