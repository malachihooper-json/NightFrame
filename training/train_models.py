#!/usr/bin/env python3
"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME NEURAL TRAINING SUITE                                 ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Training scripts for NIGHTFRAME neural models using HuggingFace datasets ║
║  Supports: RF fingerprinting, location prediction, bio-inspired patterns  ║
╚═══════════════════════════════════════════════════════════════════════════╝

Datasets:
- GDAlab/Sat2GroundScape-Panorama: Satellite-to-ground view synthesis (location)
- levinlab/neuroscience-to-dev-bio: Bio-inspired neural patterns

Usage:
    python train_models.py --dataset sat2ground --epochs 50
    python train_models.py --dataset neurobio --epochs 100
    python train_models.py --all --epochs 50
    python train_models.py --corpus path/to/corpus_chunks.jsonl --epochs 5
"""

import os
import sys
import argparse
import json
from datetime import datetime
from pathlib import Path

# Check for required packages
try:
    import torch
    import torch.nn as nn
    from torch.utils.data import DataLoader, Dataset
    from datasets import load_dataset
    import numpy as np
except ImportError as e:
    print(f"Missing dependency: {e}")
    print("Install with: pip install torch datasets numpy transformers")
    sys.exit(1)

# ═══════════════════════════════════════════════════════════════════════════════
#                              CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

MODELS_DIR = Path(__file__).parent.parent / "models"
TRAINING_DATA_DIR = Path(__file__).parent.parent / "training_data"
CHECKPOINTS_DIR = Path(__file__).parent.parent / "checkpoints"

# Ensure directories exist
MODELS_DIR.mkdir(exist_ok=True)
TRAINING_DATA_DIR.mkdir(exist_ok=True)
CHECKPOINTS_DIR.mkdir(exist_ok=True)


# ═══════════════════════════════════════════════════════════════════════════════
#                              LOCATION MODEL (Sat2Ground)
# ═══════════════════════════════════════════════════════════════════════════════

class LocationFeatureExtractor(nn.Module):
    """
    Extracts location features from satellite imagery conditions.
    Used to enhance RF fingerprinting with visual context.
    """
    def __init__(self, input_channels=3, hidden_dim=256, output_dim=64):
        super().__init__()
        
        # Convolutional encoder
        self.encoder = nn.Sequential(
            nn.Conv2d(input_channels, 32, kernel_size=3, stride=2, padding=1),
            nn.BatchNorm2d(32),
            nn.ReLU(),
            nn.Conv2d(32, 64, kernel_size=3, stride=2, padding=1),
            nn.BatchNorm2d(64),
            nn.ReLU(),
            nn.Conv2d(64, 128, kernel_size=3, stride=2, padding=1),
            nn.BatchNorm2d(128),
            nn.ReLU(),
            nn.AdaptiveAvgPool2d((4, 4))
        )
        
        # Fully connected layers
        self.fc = nn.Sequential(
            nn.Linear(128 * 4 * 4, hidden_dim),
            nn.ReLU(),
            nn.Dropout(0.3),
            nn.Linear(hidden_dim, output_dim)
        )
        
    def forward(self, x):
        x = self.encoder(x)
        x = x.view(x.size(0), -1)
        return self.fc(x)


class LocationPredictor(nn.Module):
    """
    Predicts lat/lon from combined RF + visual features.
    """
    def __init__(self, rf_dim=32, visual_dim=64, hidden_dim=128):
        super().__init__()
        
        self.visual_encoder = LocationFeatureExtractor(output_dim=visual_dim)
        
        # Combined predictor
        self.predictor = nn.Sequential(
            nn.Linear(rf_dim + visual_dim, hidden_dim),
            nn.ReLU(),
            nn.Dropout(0.2),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Linear(hidden_dim // 2, 2)  # lat, lon
        )
        
    def forward(self, rf_features, image):
        visual_features = self.visual_encoder(image)
        combined = torch.cat([rf_features, visual_features], dim=1)
        return self.predictor(combined)


class Sat2GroundDataset(Dataset):
    """Dataset wrapper for Sat2GroundScape-Panorama."""
    
    def __init__(self, hf_dataset, transform=None):
        self.dataset = hf_dataset
        self.transform = transform
        
    def __len__(self):
        return len(self.dataset)
    
    def __getitem__(self, idx):
        item = self.dataset[idx]
        
        # Get image and normalize
        image = item['condition']
        if hasattr(image, 'convert'):
            image = np.array(image.convert('RGB'))
        
        # Normalize to [0, 1] and transpose to CxHxW
        image = image.astype(np.float32) / 255.0
        image = np.transpose(image, (2, 0, 1))
        
        # Get coordinates
        lat = float(item['lat'])
        lon = float(item['lon'])
        
        # Normalize coordinates (approximate)
        lat_norm = (lat + 90) / 180.0  # [-90, 90] -> [0, 1]
        lon_norm = (lon + 180) / 360.0  # [-180, 180] -> [0, 1]
        
        return {
            'image': torch.tensor(image),
            'lat': torch.tensor(lat_norm),
            'lon': torch.tensor(lon_norm),
            'lat_raw': lat,
            'lon_raw': lon
        }


def train_location_model(epochs=50, batch_size=16, lr=1e-4):
    """Train the location prediction model on Sat2GroundScape data."""
    
    print("◈ Loading Sat2GroundScape-Panorama dataset...")
    try:
        ds = load_dataset("GDAlab/Sat2GroundScape-Panorama", split="train")
        print(f"  Loaded {len(ds)} samples")
    except Exception as e:
        print(f"∴ Failed to load dataset: {e}")
        print("  Creating synthetic data for testing...")
        return train_location_model_synthetic(epochs, batch_size, lr)
    
    # Create dataset and dataloader
    dataset = Sat2GroundDataset(ds)
    train_size = int(0.8 * len(dataset))
    val_size = len(dataset) - train_size
    train_dataset, val_dataset = torch.utils.data.random_split(dataset, [train_size, val_size])
    
    train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True, num_workers=2)
    val_loader = DataLoader(val_dataset, batch_size=batch_size, shuffle=False, num_workers=2)
    
    # Initialize model
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"◎ Using device: {device}")
    
    model = LocationFeatureExtractor(output_dim=2).to(device)  # Direct lat/lon prediction
    optimizer = torch.optim.AdamW(model.parameters(), lr=lr)
    criterion = nn.MSELoss()
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, epochs)
    
    best_val_loss = float('inf')
    
    print(f"◈ Starting training for {epochs} epochs...")
    
    for epoch in range(epochs):
        # Training
        model.train()
        train_loss = 0.0
        for batch in train_loader:
            images = batch['image'].to(device)
            targets = torch.stack([batch['lat'], batch['lon']], dim=1).to(device)
            
            optimizer.zero_grad()
            outputs = model(images)
            loss = criterion(outputs, targets)
            loss.backward()
            optimizer.step()
            
            train_loss += loss.item()
        
        train_loss /= len(train_loader)
        
        # Validation
        model.eval()
        val_loss = 0.0
        with torch.no_grad():
            for batch in val_loader:
                images = batch['image'].to(device)
                targets = torch.stack([batch['lat'], batch['lon']], dim=1).to(device)
                outputs = model(images)
                val_loss += criterion(outputs, targets).item()
        
        val_loss /= len(val_loader)
        scheduler.step()
        
        # Calculate approximate distance error (very rough)
        # Assuming normalized coords, error * 180 ≈ degrees
        approx_error_deg = np.sqrt(val_loss) * 180
        approx_error_km = approx_error_deg * 111  # ~111 km per degree
        
        print(f"  Epoch {epoch+1}/{epochs} | Train: {train_loss:.6f} | Val: {val_loss:.6f} | ~{approx_error_km:.1f}km error")
        
        # Save best model
        if val_loss < best_val_loss:
            best_val_loss = val_loss
            torch.save(model.state_dict(), MODELS_DIR / "location_model.pt")
            print(f"  ◈ Saved best model")
    
    # Export to ONNX
    print("◎ Exporting to ONNX...")
    model.eval()
    dummy_input = torch.randn(1, 3, 512, 1024).to(device)
    torch.onnx.export(
        model, dummy_input,
        str(MODELS_DIR / "location_model.onnx"),
        input_names=['image'],
        output_names=['location'],
        dynamic_axes={'image': {0: 'batch'}, 'location': {0: 'batch'}}
    )
    print(f"◈ Model saved to {MODELS_DIR / 'location_model.onnx'}")
    
    return model


def train_location_model_synthetic(epochs=50, batch_size=16, lr=1e-4):
    """Train with synthetic data when real dataset is unavailable."""
    print("◎ Training with synthetic location data...")
    
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = LocationFeatureExtractor(output_dim=2).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=lr)
    criterion = nn.MSELoss()
    
    for epoch in range(min(epochs, 10)):
        # Generate synthetic batch
        images = torch.randn(batch_size, 3, 64, 128).to(device)
        targets = torch.rand(batch_size, 2).to(device)
        
        optimizer.zero_grad()
        outputs = model(images)
        loss = criterion(outputs, targets)
        loss.backward()
        optimizer.step()
        
        print(f"  Epoch {epoch+1} | Loss: {loss.item():.6f}")
    
    torch.save(model.state_dict(), MODELS_DIR / "location_model_synthetic.pt")
    print(f"◈ Synthetic model saved")
    return model


# ═══════════════════════════════════════════════════════════════════════════════
#                              BIO-INSPIRED MODEL (Neuroscience)
# ═══════════════════════════════════════════════════════════════════════════════

class BioInspiredEncoder(nn.Module):
    """
    Encodes neuroscience concepts into embeddings for mesh behavior patterns.
    Based on Levin Lab's work on bioelectricity and morphogenesis.
    """
    def __init__(self, vocab_size=10000, embed_dim=256, hidden_dim=512, output_dim=128):
        super().__init__()
        
        self.embedding = nn.Embedding(vocab_size, embed_dim)
        
        # Transformer-style encoder
        self.encoder = nn.TransformerEncoder(
            nn.TransformerEncoderLayer(
                d_model=embed_dim,
                nhead=8,
                dim_feedforward=hidden_dim,
                dropout=0.1,
                batch_first=True
            ),
            num_layers=3
        )
        
        # Output projection
        self.output = nn.Sequential(
            nn.Linear(embed_dim, hidden_dim),
            nn.ReLU(),
            nn.Linear(hidden_dim, output_dim)
        )
        
    def forward(self, x, mask=None):
        x = self.embedding(x)
        x = self.encoder(x, src_key_padding_mask=mask)
        # Mean pooling
        x = x.mean(dim=1)
        return self.output(x)


class MeshBehaviorPredictor(nn.Module):
    """
    Predicts mesh network behavior patterns from bio-inspired encodings.
    Maps neuroscience concepts to swarm coordination strategies.
    """
    def __init__(self, input_dim=128, hidden_dim=256, num_behaviors=8):
        super().__init__()
        
        self.predictor = nn.Sequential(
            nn.Linear(input_dim, hidden_dim),
            nn.ReLU(),
            nn.Dropout(0.2),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Linear(hidden_dim // 2, num_behaviors)
        )
        
        # Behavior categories:
        # 0: Aggregate (cluster nodes)
        # 1: Disperse (spread out)
        # 2: Heal (recover from failure)
        # 3: Migrate (move to new area)
        # 4: Replicate (spawn new nodes)
        # 5: Specialize (differentiate roles)
        # 6: Communicate (increase signaling)
        # 7: Hibernate (reduce activity)
        
    def forward(self, x):
        return torch.softmax(self.predictor(x), dim=-1)


class NeuroBioDataset(Dataset):
    """Dataset wrapper for neuroscience-to-dev-bio."""
    
    def __init__(self, hf_dataset, tokenizer=None, max_length=256):
        self.dataset = hf_dataset
        self.max_length = max_length
        self.tokenizer = tokenizer or self._simple_tokenizer
        self.vocab = {}
        self._build_vocab()
        
    def _build_vocab(self):
        """Build vocabulary from dataset."""
        self.vocab = {'<PAD>': 0, '<UNK>': 1}
        for item in self.dataset:
            for key in ['input', 'output']:
                if key in item:
                    for word in str(item[key]).lower().split():
                        if word not in self.vocab:
                            self.vocab[word] = len(self.vocab)
        print(f"  Built vocabulary with {len(self.vocab)} tokens")
        
    def _simple_tokenizer(self, text):
        """Simple word tokenizer."""
        words = str(text).lower().split()[:self.max_length]
        tokens = [self.vocab.get(w, 1) for w in words]  # 1 = <UNK>
        # Pad to max_length
        tokens = tokens + [0] * (self.max_length - len(tokens))
        return tokens
        
    def __len__(self):
        return len(self.dataset)
    
    def __getitem__(self, idx):
        item = self.dataset[idx]
        
        # Get input and output text
        input_text = item.get('input', '')
        output_text = item.get('output', '')
        
        input_tokens = self.tokenizer(input_text)
        output_tokens = self.tokenizer(output_text)
        
        return {
            'input_ids': torch.tensor(input_tokens, dtype=torch.long),
            'output_ids': torch.tensor(output_tokens, dtype=torch.long),
            'input_text': input_text,
            'output_text': output_text
        }


def train_neurobio_model(epochs=100, batch_size=32, lr=1e-4):
    """Train the bio-inspired behavior model on neuroscience data."""
    
    print("◈ Loading neuroscience-to-dev-bio dataset...")
    try:
        ds = load_dataset("levinlab/neuroscience-to-dev-bio", split="train")
        print(f"  Loaded {len(ds)} samples")
    except Exception as e:
        print(f"∴ Failed to load dataset: {e}")
        print("  Skipping neurobio training...")
        return None
    
    # Create dataset
    dataset = NeuroBioDataset(ds, max_length=128)
    train_size = int(0.8 * len(dataset))
    val_size = len(dataset) - train_size
    train_dataset, val_dataset = torch.utils.data.random_split(dataset, [train_size, val_size])
    
    train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=batch_size, shuffle=False)
    
    # Initialize model
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    vocab_size = len(dataset.vocab)
    
    encoder = BioInspiredEncoder(vocab_size=vocab_size, output_dim=128).to(device)
    predictor = MeshBehaviorPredictor(input_dim=128, num_behaviors=8).to(device)
    
    # Combined parameters
    params = list(encoder.parameters()) + list(predictor.parameters())
    optimizer = torch.optim.AdamW(params, lr=lr)
    
    print(f"◎ Using device: {device}")
    print(f"◎ Vocabulary size: {vocab_size}")
    print(f"◈ Starting training for {epochs} epochs...")
    
    for epoch in range(epochs):
        encoder.train()
        predictor.train()
        train_loss = 0.0
        
        for batch in train_loader:
            input_ids = batch['input_ids'].to(device)
            
            optimizer.zero_grad()
            
            # Encode input
            encoding = encoder(input_ids)
            
            # Predict behavior (self-supervised: predict consistent patterns)
            behavior = predictor(encoding)
            
            # Loss: encourage diverse but consistent behaviors
            # Using entropy regularization
            entropy = -(behavior * torch.log(behavior + 1e-8)).sum(dim=-1).mean()
            consistency = behavior.var(dim=0).mean()  # Variance across behaviors
            
            loss = -entropy + 0.1 * consistency  # Maximize entropy, minimize variance
            loss.backward()
            optimizer.step()
            
            train_loss += loss.item()
        
        train_loss /= len(train_loader)
        
        if (epoch + 1) % 10 == 0:
            print(f"  Epoch {epoch+1}/{epochs} | Loss: {train_loss:.6f}")
    
    # Save models
    torch.save({
        'encoder': encoder.state_dict(),
        'predictor': predictor.state_dict(),
        'vocab': dataset.vocab
    }, MODELS_DIR / "neurobio_model.pt")
    
    print(f"◈ Model saved to {MODELS_DIR / 'neurobio_model.pt'}")
    
    return encoder, predictor


# ═══════════════════════════════════════════════════════════════════════════════
#                              CORPUS TEXT MODEL
# ═══════════════════════════════════════════════════════════════════════════════

class CorpusTextEncoder(nn.Module):
    """
    Text encoder for TTS corpus training.
    Creates embeddings that capture NIGHTFRAME domain knowledge.
    """
    def __init__(self, vocab_size=10000, embed_dim=256, hidden_dim=512, output_dim=128):
        super().__init__()
        
        self.embedding = nn.Embedding(vocab_size, embed_dim)
        self.lstm = nn.LSTM(embed_dim, hidden_dim, num_layers=2, 
                           batch_first=True, bidirectional=True, dropout=0.2)
        self.fc = nn.Sequential(
            nn.Linear(hidden_dim * 2, hidden_dim),
            nn.ReLU(),
            nn.Dropout(0.2),
            nn.Linear(hidden_dim, output_dim)
        )
        
    def forward(self, x):
        embedded = self.embedding(x)
        lstm_out, _ = self.lstm(embedded)
        # Use last hidden state
        last_hidden = lstm_out[:, -1, :]
        return self.fc(last_hidden)


class CorpusTextDataset(Dataset):
    """Dataset for corpus text chunks."""
    
    def __init__(self, jsonl_path, max_length=256):
        self.chunks = []
        self.max_length = max_length
        
        # Load chunks
        with open(jsonl_path, 'r', encoding='utf-8') as f:
            for line in f:
                data = json.loads(line.strip())
                self.chunks.append(data['text'])
        
        # Build vocabulary
        self.vocab = self._build_vocab()
        
    def _build_vocab(self):
        word_counts = {}
        for chunk in self.chunks:
            for word in chunk.lower().split():
                word_counts[word] = word_counts.get(word, 0) + 1
        
        # Take most frequent words
        sorted_words = sorted(word_counts.items(), key=lambda x: -x[1])
        vocab = {'<pad>': 0, '<unk>': 1}
        for word, _ in sorted_words[:9998]:  # Reserve 0, 1 for pad, unk
            vocab[word] = len(vocab)
        
        return vocab
    
    def _encode(self, text):
        tokens = [self.vocab.get(w, 1) for w in text.lower().split()]
        # Pad or truncate
        if len(tokens) < self.max_length:
            tokens += [0] * (self.max_length - len(tokens))
        else:
            tokens = tokens[:self.max_length]
        return tokens
    
    def __len__(self):
        return max(1, len(self.chunks) - 1)  # Pairs of consecutive chunks
    
    def __getitem__(self, idx):
        # Self-supervised: predict next chunk's embedding from current
        current = self._encode(self.chunks[idx])
        next_chunk = self._encode(self.chunks[idx + 1] if idx + 1 < len(self.chunks) else self.chunks[0])
        return {
            'input': torch.tensor(current),
            'target': torch.tensor(next_chunk)
        }


def train_corpus_model(corpus_path, epochs=5, batch_size=8, lr=1e-4, output_path=None):
    """Train on TTS corpus text for domain-specific embeddings."""
    
    print(f"◈ Loading corpus from {corpus_path}...")
    
    try:
        dataset = CorpusTextDataset(corpus_path)
        print(f"  Loaded {len(dataset.chunks)} chunks, vocab size: {len(dataset.vocab)}")
    except Exception as e:
        print(f"∴ Failed to load corpus: {e}")
        return None
    
    if len(dataset.chunks) < 2:
        print("∴ Need at least 2 chunks for training")
        return None
    
    dataloader = DataLoader(dataset, batch_size=batch_size, shuffle=True)
    
    # Initialize model
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"◎ Using device: {device}")
    
    vocab_size = len(dataset.vocab)
    model = CorpusTextEncoder(vocab_size=vocab_size).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=lr)
    criterion = nn.MSELoss()
    
    print(f"◈ Starting corpus training for {epochs} epochs...")
    
    for epoch in range(epochs):
        model.train()
        total_loss = 0.0
        
        for batch in dataloader:
            inputs = batch['input'].to(device)
            targets = batch['target'].to(device)
            
            optimizer.zero_grad()
            
            # Encode both input and target
            input_embed = model(inputs)
            
            # Create pseudo-target by encoding target sequence
            with torch.no_grad():
                target_embed = model(targets)
            
            # Loss: embedding similarity
            loss = criterion(input_embed, target_embed)
            loss.backward()
            optimizer.step()
            
            total_loss += loss.item()
        
        avg_loss = total_loss / len(dataloader)
        print(f"  Epoch {epoch+1}/{epochs} - Loss: {avg_loss:.4f}")
    
    # Save model
    save_path = output_path or (MODELS_DIR / "corpus_encoder.pt")
    Path(save_path).parent.mkdir(parents=True, exist_ok=True)
    
    torch.save({
        'model': model.state_dict(),
        'vocab': dataset.vocab,
        'vocab_size': vocab_size
    }, save_path)
    
    print(f"◈ Corpus model saved to {save_path}")
    return model


# ═══════════════════════════════════════════════════════════════════════════════
#                              MAIN ENTRY POINT
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(description="NIGHTFRAME Neural Training Suite")
    parser.add_argument('--dataset', choices=['sat2ground', 'neurobio', 'all'], 
                        default='all', help='Which dataset to train on')
    parser.add_argument('--corpus', type=str, default=None,
                        help='Path to corpus JSONL file for text training')
    parser.add_argument('--output', type=str, default=None,
                        help='Output path for trained model')
    parser.add_argument('--epochs', type=int, default=50, help='Number of training epochs')
    parser.add_argument('--batch-size', type=int, default=16, help='Batch size')
    parser.add_argument('--lr', type=float, default=1e-4, help='Learning rate')
    
    args = parser.parse_args()
    
    print("═══════════════════════════════════════════════════════════════════")
    print("◈ NIGHTFRAME NEURAL TRAINING SUITE")
    print("═══════════════════════════════════════════════════════════════════")
    print(f"  Device: {'CUDA' if torch.cuda.is_available() else 'CPU'}")
    print(f"  Models directory: {MODELS_DIR}")
    print(f"  Epochs: {args.epochs}")
    print(f"  Batch size: {args.batch_size}")
    print("═══════════════════════════════════════════════════════════════════")
    print()
    
    # Handle corpus training mode
    if args.corpus:
        print("\n┌────────────────────────────────────────────────────────────────┐")
        print("│ Training Corpus Text Model                                     │")
        print("└────────────────────────────────────────────────────────────────┘\n")
        train_corpus_model(args.corpus, epochs=args.epochs, batch_size=args.batch_size, 
                          lr=args.lr, output_path=args.output)
    else:
        if args.dataset in ['sat2ground', 'all']:
            print("\n┌────────────────────────────────────────────────────────────────┐")
            print("│ Training Location Model (Sat2GroundScape-Panorama)            │")
            print("└────────────────────────────────────────────────────────────────┘\n")
            train_location_model(epochs=args.epochs, batch_size=args.batch_size, lr=args.lr)
    
        if args.dataset in ['neurobio', 'all']:
            print("\n┌────────────────────────────────────────────────────────────────┐")
            print("│ Training Bio-Inspired Model (neuroscience-to-dev-bio)         │")
            print("└────────────────────────────────────────────────────────────────┘\n")
            train_neurobio_model(epochs=args.epochs, batch_size=args.batch_size, lr=args.lr)
    
    print("\n═══════════════════════════════════════════════════════════════════")
    print("◈ TRAINING COMPLETE")
    print("═══════════════════════════════════════════════════════════════════")
    
    # List saved models
    print("\nSaved models:")
    for model_file in MODELS_DIR.glob("*.pt"):
        size_mb = model_file.stat().st_size / (1024 * 1024)
        print(f"  • {model_file.name} ({size_mb:.2f} MB)")
    for model_file in MODELS_DIR.glob("*.onnx"):
        size_mb = model_file.stat().st_size / (1024 * 1024)
        print(f"  • {model_file.name} ({size_mb:.2f} MB)")


if __name__ == "__main__":
    main()
