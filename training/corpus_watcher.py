#!/usr/bin/env python3
"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME CORPUS WATCHER                                        ║
╠═══════════════════════════════════════════════════════════════════════════╣
║  Monitors TTS corpus files and auto-triggers neural network training.      ║
║  Uses watchdog for file system monitoring with debouncing.                 ║
╚═══════════════════════════════════════════════════════════════════════════╝

Usage:
    python corpus_watcher.py                    # Watch with default settings
    python corpus_watcher.py --once             # Process corpus once (no watch)
    python corpus_watcher.py --debounce 10      # Custom debounce (seconds)
"""

import os
import sys
import time
import argparse
import hashlib
import subprocess
from pathlib import Path
from datetime import datetime
from threading import Timer

# Check for watchdog
try:
    from watchdog.observers import Observer
    from watchdog.events import FileSystemEventHandler
except ImportError:
    print("Missing watchdog library. Installing...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "watchdog"])
    from watchdog.observers import Observer
    from watchdog.events import FileSystemEventHandler

# ═══════════════════════════════════════════════════════════════════════════════
#                              CONFIGURATION
# ═══════════════════════════════════════════════════════════════════════════════

PROJECT_ROOT = Path(__file__).parent.parent
CORPUS_PATH = PROJECT_ROOT / "README_TTS_CORPUS.md"
TRAINING_SCRIPT = PROJECT_ROOT / "training" / "train_models.py"
MODELS_DIR = PROJECT_ROOT / "models"
LOGS_DIR = PROJECT_ROOT / "training" / "logs"

# Create directories
LOGS_DIR.mkdir(exist_ok=True)
MODELS_DIR.mkdir(exist_ok=True)


# ═══════════════════════════════════════════════════════════════════════════════
#                              CORPUS PROCESSOR
# ═══════════════════════════════════════════════════════════════════════════════

class CorpusProcessor:
    """Processes TTS corpus text for neural network training."""
    
    def __init__(self, corpus_path: Path):
        self.corpus_path = corpus_path
        self.last_hash: str | None = None
        
    def get_corpus_hash(self) -> str:
        """Get MD5 hash of corpus file."""
        with open(self.corpus_path, 'rb') as f:
            return hashlib.md5(f.read()).hexdigest()
    
    def has_changed(self) -> bool:
        """Check if corpus has changed since last check."""
        current_hash = self.get_corpus_hash()
        changed = current_hash != self.last_hash
        self.last_hash = current_hash
        return changed
    
    def extract_text(self) -> str:
        """Extract training text from corpus markdown."""
        with open(self.corpus_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Remove YAML frontmatter
        if content.startswith('---'):
            end_idx = content.find('---', 3)
            if end_idx != -1:
                content = content[end_idx + 3:].strip()
        
        # Remove markdown headers but keep text
        lines = []
        for line in content.split('\n'):
            line = line.strip()
            # Skip empty lines
            if not line:
                lines.append('')
                continue
            # Convert headers to sentences
            if line.startswith('#'):
                line = line.lstrip('#').strip()
                if line and not line.endswith('.'):
                    line += '.'
            # Skip horizontal rules
            if line.startswith('---') or line.startswith('==='):
                continue
            # Skip table formatting lines
            if line.startswith('|') and '-' in line:
                continue
            lines.append(line)
        
        return '\n'.join(lines)
    
    def chunk_text(self, text: str, chunk_size: int = 512, overlap: int = 64) -> list[str]:
        """Split text into overlapping chunks for training."""
        words = text.split()
        chunks = []
        
        i = 0
        while i < len(words):
            chunk_words = words[i:i + chunk_size]
            chunks.append(' '.join(chunk_words))
            i += chunk_size - overlap
            
        return chunks
    
    def save_training_data(self) -> Path:
        """Extract and save training data from corpus."""
        text = self.extract_text()
        chunks = self.chunk_text(text)
        
        # Save as JSONL for training
        output_path = PROJECT_ROOT / "training_data" / "corpus_chunks.jsonl"
        output_path.parent.mkdir(exist_ok=True)
        
        import json
        with open(output_path, 'w', encoding='utf-8') as f:
            for i, chunk in enumerate(chunks):
                f.write(json.dumps({
                    "id": i,
                    "text": chunk,
                    "source": "README_TTS_CORPUS.md"
                }) + '\n')
        
        print(f"◎ Extracted {len(chunks)} training chunks from corpus")
        return output_path


# ═══════════════════════════════════════════════════════════════════════════════
#                              TRAINING TRIGGER
# ═══════════════════════════════════════════════════════════════════════════════

class TrainingTrigger:
    """Triggers neural network training."""
    
    def __init__(self, processor: CorpusProcessor):
        self.processor = processor
        self.is_training = False
    
    def trigger_training(self):
        """Trigger training on corpus data."""
        if self.is_training:
            print("◎ Training already in progress, skipping...")
            return
        
        self.is_training = True
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = LOGS_DIR / f"training_{timestamp}.log"
        
        try:
            print(f"\n{'='*60}")
            print(f"◎ TRAINING TRIGGERED at {datetime.now().isoformat()}")
            print(f"{'='*60}\n")
            
            # Extract training data from corpus
            training_data = self.processor.save_training_data()
            
            # Run training script with corpus flag
            print(f"◎ Running training script...")
            print(f"◎ Log file: {log_file}")
            
            with open(log_file, 'w') as log:
                result = subprocess.run(
                    [
                        sys.executable,
                        str(TRAINING_SCRIPT),
                        "--corpus",
                        str(training_data),
                        "--epochs", "5",  # Quick training for corpus updates
                        "--output", str(MODELS_DIR / f"corpus_model_{timestamp}")
                    ],
                    stdout=log,
                    stderr=subprocess.STDOUT,
                    cwd=str(PROJECT_ROOT / "training"),
                    timeout=3600  # 1 hour timeout
                )
            
            if result.returncode == 0:
                print(f"◎ Training completed successfully!")
                print(f"◎ Model saved to: {MODELS_DIR / f'corpus_model_{timestamp}'}")
            else:
                print(f"◎ Training failed with return code {result.returncode}")
                print(f"◎ Check log file for details: {log_file}")
                
        except subprocess.TimeoutExpired:
            print("◎ Training timed out after 1 hour")
        except Exception as e:
            print(f"◎ Training error: {e}")
        finally:
            self.is_training = False
            print()


# ═══════════════════════════════════════════════════════════════════════════════
#                              FILE WATCHER
# ═══════════════════════════════════════════════════════════════════════════════

class CorpusWatchHandler(FileSystemEventHandler):
    """Handles file system events for corpus watching."""
    
    def __init__(self, processor: CorpusProcessor, trigger: TrainingTrigger, debounce_seconds: float = 5.0):
        super().__init__()
        self.processor = processor
        self.trigger = trigger
        self.debounce_seconds = debounce_seconds
        self.debounce_timer: Timer | None = None
        
    def on_modified(self, event):
        """Called when a file is modified."""
        if event.is_directory:
            return
            
        # Check if it's the corpus file
        modified_path = Path(event.src_path).resolve()
        corpus_path = self.processor.corpus_path.resolve()
        
        if modified_path == corpus_path:
            print(f"◎ Corpus modification detected: {event.src_path}")
            self._debounced_trigger()
    
    def _debounced_trigger(self):
        """Trigger training with debouncing to avoid rapid-fire triggers."""
        # Cancel any pending timer
        if self.debounce_timer is not None:
            self.debounce_timer.cancel()
        
        # Start new timer
        print(f"◎ Waiting {self.debounce_seconds}s for additional changes...")
        self.debounce_timer = Timer(self.debounce_seconds, self._do_trigger)
        self.debounce_timer.start()
    
    def _do_trigger(self):
        """Actually trigger training after debounce."""
        if self.processor.has_changed():
            self.trigger.trigger_training()
        else:
            print("◎ No actual content change detected, skipping training")


def watch_corpus(debounce_seconds: float = 5.0):
    """Watch corpus file and trigger training on changes."""
    if not CORPUS_PATH.exists():
        print(f"Error: Corpus file not found: {CORPUS_PATH}")
        sys.exit(1)
    
    processor = CorpusProcessor(CORPUS_PATH)
    trigger = TrainingTrigger(processor)
    handler = CorpusWatchHandler(processor, trigger, debounce_seconds)
    
    # Initialize hash
    processor.last_hash = processor.get_corpus_hash()
    
    observer = Observer()
    observer.schedule(handler, str(CORPUS_PATH.parent), recursive=False)
    observer.start()
    
    print(f"""
╔═══════════════════════════════════════════════════════════════════════════╗
║           NIGHTFRAME CORPUS WATCHER ACTIVE                                 ║
╚═══════════════════════════════════════════════════════════════════════════╝

Watching: {CORPUS_PATH}
Debounce: {debounce_seconds} seconds
Training script: {TRAINING_SCRIPT}

Press Ctrl+C to stop watching.
""")
    
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n◎ Stopping watcher...")
        observer.stop()
    
    observer.join()


def process_once():
    """Process corpus once without watching."""
    if not CORPUS_PATH.exists():
        print(f"Error: Corpus file not found: {CORPUS_PATH}")
        sys.exit(1)
    
    processor = CorpusProcessor(CORPUS_PATH)
    trigger = TrainingTrigger(processor)
    
    processor.save_training_data()
    trigger.trigger_training()


# ═══════════════════════════════════════════════════════════════════════════════
#                              MAIN
# ═══════════════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Watch TTS corpus and auto-trigger training on changes"
    )
    parser.add_argument(
        "--once",
        action="store_true",
        help="Process corpus once without watching"
    )
    parser.add_argument(
        "--debounce",
        type=float,
        default=5.0,
        help="Debounce time in seconds (default: 5)"
    )
    
    args = parser.parse_args()
    
    if args.once:
        process_once()
    else:
        watch_corpus(args.debounce)
