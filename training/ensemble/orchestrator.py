"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME ENSEMBLE TRAINING ORCHESTRATOR                        ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Manages ensemble of training transformations with automatic adaptation.   ║
║  Implements CNN-LSTM hybrid for RF fingerprinting based on research.       ║
╚═══════════════════════════════════════════════════════════════════════════╝

Based on techniques from:
- RF Deep Learning papers (CNN I/Q processing, LSTM RSS sequences)
- Hybrid CNN-LSTM for spatial-temporal learning
- Attention mechanisms for signal focus
"""

import torch
import torch.nn as nn
import torch.nn.functional as F
import numpy as np
import logging
from datetime import datetime
from pathlib import Path
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Any, Tuple
import json

logger = logging.getLogger("NIGHTFRAME.Ensemble")

# ═══════════════════════════════════════════════════════════════════════════════
#                              MODEL ARCHITECTURES
# ═══════════════════════════════════════════════════════════════════════════════

class RFCNNModel(nn.Module):
    """
    CNN for spatial RF pattern extraction.
    
    Processes RSSI/CSI matrices to extract spatial features.
    Based on I/Q sample processing from RF fingerprinting research.
    """
    
    def __init__(self, input_channels: int = 2, hidden_dim: int = 128, output_dim: int = 64):
        super().__init__()
        
        self.conv1 = nn.Conv1d(input_channels, 32, kernel_size=7, padding=3)
        self.bn1 = nn.BatchNorm1d(32)
        self.conv2 = nn.Conv1d(32, 64, kernel_size=5, padding=2)
        self.bn2 = nn.BatchNorm1d(64)
        self.conv3 = nn.Conv1d(64, hidden_dim, kernel_size=3, padding=1)
        self.bn3 = nn.BatchNorm1d(hidden_dim)
        
        self.pool = nn.AdaptiveAvgPool1d(1)
        self.fc = nn.Linear(hidden_dim, output_dim)
        self.dropout = nn.Dropout(0.3)
        
    def forward(self, x: torch.Tensor) -> torch.Tensor:
        # x: (batch, channels, sequence_length)
        x = F.relu(self.bn1(self.conv1(x)))
        x = F.relu(self.bn2(self.conv2(x)))
        x = F.relu(self.bn3(self.conv3(x)))
        x = self.pool(x).squeeze(-1)
        x = self.dropout(x)
        x = self.fc(x)
        return x


class RFLSTMModel(nn.Module):
    """
    LSTM for temporal signal dynamics.
    
    Captures time-series patterns in RSS sequences.
    """
    
    def __init__(self, input_dim: int = 8, hidden_dim: int = 128, 
                 num_layers: int = 2, output_dim: int = 64):
        super().__init__()
        
        self.lstm = nn.LSTM(
            input_dim, hidden_dim, 
            num_layers=num_layers, 
            batch_first=True,
            dropout=0.2 if num_layers > 1 else 0,
            bidirectional=True
        )
        
        self.fc = nn.Linear(hidden_dim * 2, output_dim)  # *2 for bidirectional
        self.dropout = nn.Dropout(0.3)
        
    def forward(self, x: torch.Tensor) -> torch.Tensor:
        # x: (batch, sequence_length, features)
        lstm_out, (h_n, c_n) = self.lstm(x)
        
        # Use final hidden states from both directions
        h_forward = h_n[-2]  # Last forward layer
        h_backward = h_n[-1]  # Last backward layer
        h_combined = torch.cat([h_forward, h_backward], dim=1)
        
        x = self.dropout(h_combined)
        x = self.fc(x)
        return x


class AttentionLayer(nn.Module):
    """
    Self-attention for focusing on relevant signal features.
    """
    
    def __init__(self, embed_dim: int = 64, num_heads: int = 4):
        super().__init__()
        self.attention = nn.MultiheadAttention(embed_dim, num_heads, batch_first=True)
        self.norm = nn.LayerNorm(embed_dim)
        
    def forward(self, x: torch.Tensor) -> torch.Tensor:
        attn_out, _ = self.attention(x, x, x)
        x = self.norm(x + attn_out)
        return x


class HybridCNNLSTM(nn.Module):
    """
    Hybrid CNN-LSTM architecture for spatial-temporal RF learning.
    
    Architecture:
    1. CNN extracts local spatial patterns
    2. LSTM captures temporal dependencies
    3. Attention focuses on important features
    4. Fusion layer combines all information
    """
    
    def __init__(self, 
                 input_channels: int = 2,
                 sequence_length: int = 100,
                 hidden_dim: int = 128,
                 output_dim: int = 2):  # lat, lon
        super().__init__()
        
        self.sequence_length = sequence_length
        
        # CNN branch for spatial features
        self.cnn = RFCNNModel(input_channels, hidden_dim, 64)
        
        # LSTM branch for temporal features
        self.lstm = RFLSTMModel(input_channels, hidden_dim, 2, 64)
        
        # Attention for focusing (matches combined feature size: 64+64=128)
        self.attention = AttentionLayer(128, 4)
        
        # Fusion layer
        self.fusion = nn.Sequential(
            nn.Linear(128, 128),  # 64 + 64 from CNN and LSTM
            nn.ReLU(),
            nn.Dropout(0.3),
            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Linear(64, output_dim)
        )
        
    def forward(self, x: torch.Tensor) -> torch.Tensor:
        """
        Args:
            x: Input tensor of shape (batch, channels, sequence_length)
               or (batch, sequence_length, features)
        """
        # Ensure correct shape for CNN (batch, channels, seq)
        if x.dim() == 2:
            x = x.unsqueeze(1)  # Add channel dim
        
        if x.shape[1] > x.shape[2]:  # Likely (batch, seq, features)
            x_cnn = x.permute(0, 2, 1)
        else:
            x_cnn = x
        
        # CNN features
        cnn_features = self.cnn(x_cnn)
        
        # LSTM features (needs batch, seq, features)
        if x.shape[1] > x.shape[2]:
            x_lstm = x
        else:
            x_lstm = x.permute(0, 2, 1)
        
        lstm_features = self.lstm(x_lstm)
        
        # Combine features
        combined = torch.cat([cnn_features, lstm_features], dim=1)
        
        # Apply attention
        combined = combined.unsqueeze(1)  # Add sequence dimension for attention
        attended = self.attention(combined)
        attended = attended.squeeze(1)
        
        # Output
        output = self.fusion(attended)
        
        return output


class NetworkHandoverPredictor(nn.Module):
    """
    LSTM-based model for predicting network handovers.
    
    Predicts:
    - Probability of handover in next N seconds
    - Optimal target cell
    - Expected quality after handover
    """
    
    def __init__(self, input_dim: int = 12, hidden_dim: int = 64, num_classes: int = 10):
        super().__init__()
        
        self.lstm = nn.LSTM(input_dim, hidden_dim, num_layers=2, batch_first=True, dropout=0.2)
        
        # Handover probability head
        self.handover_head = nn.Sequential(
            nn.Linear(hidden_dim, 32),
            nn.ReLU(),
            nn.Linear(32, 1),
            nn.Sigmoid()
        )
        
        # Target cell head
        self.target_head = nn.Sequential(
            nn.Linear(hidden_dim, 32),
            nn.ReLU(),
            nn.Linear(32, num_classes)
        )
        
        # Quality prediction head
        self.quality_head = nn.Sequential(
            nn.Linear(hidden_dim, 32),
            nn.ReLU(),
            nn.Linear(32, 1),
            nn.Sigmoid()
        )
        
    def forward(self, x: torch.Tensor) -> Dict[str, torch.Tensor]:
        _, (h_n, _) = self.lstm(x)
        features = h_n[-1]
        
        return {
            "handover_prob": self.handover_head(features),
            "target_cell": self.target_head(features),
            "quality_pred": self.quality_head(features)
        }


# ═══════════════════════════════════════════════════════════════════════════════
#                              TRAINING ORCHESTRATOR
# ═══════════════════════════════════════════════════════════════════════════════

@dataclass
class TrainingConfig:
    """Configuration for ensemble training."""
    batch_size: int = 32
    learning_rate: float = 0.001
    epochs: int = 100
    early_stopping_patience: int = 10
    device: str = "cuda" if torch.cuda.is_available() else "cpu"
    
    # Ensemble weights (learned during training)
    cnn_weight: float = 0.3
    lstm_weight: float = 0.3
    hybrid_weight: float = 0.4


@dataclass
class TrainingResult:
    """Result of a training run."""
    model_name: str
    domain: str
    epochs_completed: int
    final_loss: float
    final_accuracy: float
    best_epoch: int
    training_time_seconds: float
    model_path: Optional[str] = None


class TrainingOrchestrator:
    """
    Orchestrates ensemble training with metacognitive feedback.
    
    Manages multiple model architectures and automatically
    adapts training based on performance.
    """
    
    def __init__(self, 
                 models_path: Path = None,
                 config: TrainingConfig = None):
        self.models_path = models_path or Path("models/ensemble")
        self.models_path.mkdir(parents=True, exist_ok=True)
        
        self.config = config or TrainingConfig()
        self.device = torch.device(self.config.device)
        
        # Initialize ensemble models
        self.models: Dict[str, nn.Module] = {}
        self.optimizers: Dict[str, torch.optim.Optimizer] = {}
        self.training_history: List[TrainingResult] = []
        
        # Metacognitive callbacks
        self._on_training_complete: List[callable] = []
        self._on_skill_learned: List[callable] = []
        
        logger.info(f"◈ TrainingOrchestrator initialized (device: {self.device})")
    
    def initialize_models(self):
        """Initialize all ensemble models."""
        
        # Location prediction (RF fingerprinting)
        self.models["rf_location"] = HybridCNNLSTM(
            input_channels=2,
            sequence_length=100,
            hidden_dim=128,
            output_dim=2  # lat, lon
        ).to(self.device)
        
        # Handover prediction
        self.models["handover"] = NetworkHandoverPredictor(
            input_dim=12,  # RSRP, RSRQ, SINR, CellID, neighbors...
            hidden_dim=64,
            num_classes=10
        ).to(self.device)
        
        # Signal quality CNN
        self.models["signal_quality"] = RFCNNModel(
            input_channels=2,
            hidden_dim=64,
            output_dim=4  # Quality categories
        ).to(self.device)
        
        # Temporal pattern LSTM
        self.models["temporal"] = RFLSTMModel(
            input_dim=8,
            hidden_dim=128,
            output_dim=8  # Behavior categories
        ).to(self.device)
        
        # Create optimizers
        for name, model in self.models.items():
            self.optimizers[name] = torch.optim.Adam(
                model.parameters(), 
                lr=self.config.learning_rate
            )
        
        logger.info(f"◎ Initialized {len(self.models)} ensemble models")
    
    def train_model(self, 
                    model_name: str,
                    train_data: torch.Tensor,
                    train_labels: torch.Tensor,
                    val_data: Optional[torch.Tensor] = None,
                    val_labels: Optional[torch.Tensor] = None,
                    domain: str = "general") -> TrainingResult:
        """
        Train a single model in the ensemble.
        """
        if model_name not in self.models:
            raise ValueError(f"Unknown model: {model_name}")
        
        model = self.models[model_name]
        optimizer = self.optimizers[model_name]
        
        model.train()
        best_loss = float('inf')
        best_epoch = 0
        patience_counter = 0
        
        start_time = datetime.utcnow()
        
        # Training loop
        for epoch in range(self.config.epochs):
            epoch_loss = 0.0
            n_batches = 0
            
            # Mini-batch training
            for i in range(0, len(train_data), self.config.batch_size):
                batch_x = train_data[i:i+self.config.batch_size].to(self.device)
                batch_y = train_labels[i:i+self.config.batch_size].to(self.device)
                
                optimizer.zero_grad()
                
                output = model(batch_x)
                
                # Handle different output types
                if isinstance(output, dict):
                    loss = sum(F.mse_loss(v, batch_y) for v in output.values()) / len(output)
                else:
                    loss = F.mse_loss(output, batch_y)
                
                loss.backward()
                optimizer.step()
                
                epoch_loss += loss.item()
                n_batches += 1
            
            avg_loss = epoch_loss / max(n_batches, 1)
            
            # Early stopping check
            if avg_loss < best_loss:
                best_loss = avg_loss
                best_epoch = epoch
                patience_counter = 0
                
                # Save best model
                self._save_model(model_name, model, domain)
            else:
                patience_counter += 1
                if patience_counter >= self.config.early_stopping_patience:
                    logger.info(f"◎ Early stopping at epoch {epoch}")
                    break
            
            if epoch % 10 == 0:
                logger.info(f"◎ {model_name} epoch {epoch}: loss={avg_loss:.4f}")
        
        training_time = (datetime.utcnow() - start_time).total_seconds()
        
        # Compute final accuracy if we have validation data
        accuracy = 0.0
        if val_data is not None:
            model.eval()
            with torch.no_grad():
                val_out = model(val_data.to(self.device))
                if isinstance(val_out, dict):
                    val_out = list(val_out.values())[0]
                accuracy = 1.0 - F.mse_loss(val_out, val_labels.to(self.device)).item()
        
        result = TrainingResult(
            model_name=model_name,
            domain=domain,
            epochs_completed=epoch + 1,
            final_loss=best_loss,
            final_accuracy=max(0, accuracy),
            best_epoch=best_epoch,
            training_time_seconds=training_time,
            model_path=str(self.models_path / f"{model_name}_{domain}.pt")
        )
        
        self.training_history.append(result)
        
        # Notify callbacks
        for callback in self._on_training_complete:
            try:
                callback(result)
            except Exception as e:
                logger.error(f"Callback error: {e}")
        
        logger.info(f"◈ Training complete: {model_name} (loss={best_loss:.4f})")
        
        return result
    
    def train_ensemble(self,
                       data: Dict[str, Tuple[torch.Tensor, torch.Tensor]],
                       domain: str = "cellular") -> List[TrainingResult]:
        """
        Train all models in the ensemble.
        
        Args:
            data: Dict mapping model_name -> (train_data, train_labels)
            domain: Capability domain
        """
        results = []
        
        for model_name, (train_data, train_labels) in data.items():
            if model_name in self.models:
                result = self.train_model(model_name, train_data, train_labels, domain=domain)
                results.append(result)
        
        return results
    
    def predict_ensemble(self, 
                         x: torch.Tensor,
                         model_names: Optional[List[str]] = None) -> Dict[str, torch.Tensor]:
        """
        Get predictions from ensemble members.
        """
        if model_names is None:
            model_names = list(self.models.keys())
        
        predictions = {}
        
        for name in model_names:
            if name in self.models:
                model = self.models[name]
                model.eval()
                
                with torch.no_grad():
                    pred = model(x.to(self.device))
                    predictions[name] = pred
        
        return predictions
    
    def fuse_predictions(self, 
                         predictions: Dict[str, torch.Tensor]) -> torch.Tensor:
        """
        Fuse ensemble predictions using learned weights.
        """
        weights = {
            "rf_location": self.config.hybrid_weight,
            "signal_quality": self.config.cnn_weight,
            "temporal": self.config.lstm_weight,
        }
        
        total_weight = 0
        fused = None
        
        for name, pred in predictions.items():
            if isinstance(pred, dict):
                pred = list(pred.values())[0]
            
            weight = weights.get(name, 0.25)
            
            if fused is None:
                fused = pred * weight
            else:
                # Handle shape mismatch
                if fused.shape == pred.shape:
                    fused = fused + pred * weight
            
            total_weight += weight
        
        if fused is not None and total_weight > 0:
            fused = fused / total_weight
        
        return fused
    
    def _save_model(self, name: str, model: nn.Module, domain: str):
        """Save model checkpoint."""
        path = self.models_path / f"{name}_{domain}.pt"
        torch.save({
            "model_state": model.state_dict(),
            "name": name,
            "domain": domain,
            "timestamp": datetime.utcnow().isoformat()
        }, path)
    
    def load_model(self, name: str, domain: str) -> bool:
        """Load model from checkpoint."""
        path = self.models_path / f"{name}_{domain}.pt"
        
        if not path.exists():
            return False
        
        if name not in self.models:
            return False
        
        try:
            checkpoint = torch.load(path, map_location=self.device)
            self.models[name].load_state_dict(checkpoint["model_state"])
            logger.info(f"◎ Loaded model: {name}_{domain}")
            return True
        except Exception as e:
            logger.error(f"∴ Failed to load model: {e}")
            return False
    
    def on_training_complete(self, callback: callable):
        """Register callback for training completion."""
        self._on_training_complete.append(callback)
    
    def on_skill_learned(self, callback: callable):
        """Register callback for skill learning."""
        self._on_skill_learned.append(callback)
    
    def get_training_stats(self) -> Dict[str, Any]:
        """Get training statistics."""
        return {
            "models_initialized": len(self.models),
            "training_runs": len(self.training_history),
            "total_training_time": sum(r.training_time_seconds for r in self.training_history),
            "best_accuracies": {
                r.model_name: r.final_accuracy 
                for r in self.training_history
            },
            "device": str(self.device)
        }


# ═══════════════════════════════════════════════════════════════════════════════
#                              SINGLETON
# ═══════════════════════════════════════════════════════════════════════════════

_orchestrator_instance: Optional[TrainingOrchestrator] = None

def get_training_orchestrator() -> TrainingOrchestrator:
    """Get singleton TrainingOrchestrator instance."""
    global _orchestrator_instance
    if _orchestrator_instance is None:
        _orchestrator_instance = TrainingOrchestrator()
        _orchestrator_instance.initialize_models()
    return _orchestrator_instance
