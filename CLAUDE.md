# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

千千 is a Windows desktop pet — a pixel Bichon Frise dog floating on the desktop. Users chat with it, and it forwards messages to `claude -p` behind the scenes. 千千 has its own personality (SOUL.MD), a memory system, voice input (Windows STT), and voice output (Edge TTS cute voice).

- **Stack:** .NET 8, C# 12, WPF + WinForms, Newtonsoft.Json, NAudio, System.Speech
- **Entry:** `src/ClaudePet/App.xaml.cs`
- **Pet name:** 千千 (`config.json` → `PetName`)
- **No git, no tests**

## Build & Run

```bash
dotnet build claude-pet.sln
# Launch from File Explorer (bash cannot render GUI):
# D:\dev\claude-pet\src\ClaudePet\bin\Debug\net8.0-windows\ClaudePet.exe
```

## Architecture — Prompt Flow (Critical)

This is the most important thing to understand. Every user message goes through this pipeline:

```
User types in BubbleOverlay
  → PetWindow: ChatBubble.MessageSent
    → MemoryEngine.SendWithMemory(msg)
      → 1. Read soul.md (fresh each time, hot-reload)
      → 2. ContextBuilder.BuildPrefix(index) — memory context
      → 3. Append "---\n[用户说] {msg}"
      → ClaudeBridge.SendPrompt(fullPrompt)
        → ProcessManager: claude -p "<prompt>"  (NO --continue)
```

**`--continue` was REMOVED** because it accumulated old polluted context (旺财, Claude references). Each message is a fresh `claude -p` call. Memory continuity comes from the memory system injecting summaries.

## Personality System (SOUL.MD)

`D:\dev\claude-pet\memory\soul.md` is injected at the START of every prompt. It defines 千千's identity with strict prohibitions:

- 千千 is 千千 — a pixel Bichon dog, NOT an AI assistant
- Uses puppy language: 汪～、嗷呜～
- **Absolutely forbidden:** Claude, GPT, AI模型, 语言模型, WPF, 技术栈, 桌面宠物应用
- Hot-reload: MemoryEngine re-reads soul.md on every message (no restart needed)

**Test with:** `claude -p "$(cat memory/soul.md)\n\n---\n[用户说] 你是谁？"`

## Memory System

All memory files under `D:\dev\claude-pet\memory\`:

| File | Content | Loaded by |
|------|---------|-----------|
| `soul.md` | 千千's identity (always injected first) | MemoryEngine (every msg) |
| `preferences.md` | User prefs (`- ` bullets) | MemoryLoader → ParseBulletFile |
| `knowledge.md` | Project knowledge (`- ` bullets) | MemoryLoader → ParseBulletFile |
| `tasks.md` | Active tasks (`- ` bullets) | MemoryLoader → ParseBulletFile |
| `conversation/YYYY-MM-DD.md` | Daily chat summaries | MemoryLoader → ReadLastLines(15) |
| `compressed/` | Archived old convos | Compressor |
| `INDEX.md` | Timeline index | IdleOrganizer |

### Memory Pollution Protection

- **BannedWords filter** in `MemoryLoader.ParseBulletFile`: skips entries containing "旺财", "Claude Code", "Claude大脑" etc.
- **ReadLastLines only**: loads last 15 lines per conversation file (not 200+)
- **ExtractSummary** extracts only user question part from conversation entries (not 千千's verbose replies)
- **Default knowledge.md** no longer mentions "Claude Code"
- **Placeholder filter** in `ContextBuilder`: skips content containing "暂无", "（暂时没有）"

### Prompt Construction (ContextBuilder)

`ContextBuilder.BuildPrefix()` assembles `[上下文]` from:
1. Tasks (non-placeholder) — ~200 chars
2. Preferences — ~200 chars
3. Knowledge — ~300 chars
4. Recent conversations (last 2 days, last 15 lines each) — ~300 chars

Returns empty string if no real content. Total prefix ~1000 chars max.

## Voice System

- **Wake word:** `Ctrl+Shift+W` (global, Win32 `GetAsyncKeyState`)
- **STT:** `System.Speech.Recognition.SpeechRecognizer` (shared, zh-CN). Requires Windows Chinese speech pack.
- **TTS:** Edge TTS `zh-CN-XiaoxiaoNeural` with `pitch +15%` `rate -10%` for cute voice. Falls back to Windows Huihui.

## Key Files Map

```
src/ClaudePet/
├── App.xaml.cs              # Composition root
├── Memory/
│   ├── MemoryEngine.cs      # SendWithMemory() — soul + context + user msg
│   ├── ContextBuilder.cs    # BuildPrefix() — assembles [上下文]
│   ├── MemoryLoader.cs      # Load() — reads memory/*.md → MemoryIndex
│   ├── MemoryWriter.cs      # WriteConversation/UpdatePreferences etc.
│   ├── Compressor.cs        # Context >70% → compress
│   ├── IdleOrganizer.cs     # Idle >5min → organize
│   └── MemoryModels.cs      # MemoryEntry, MemoryIndex
├── Bridge/
│   ├── ClaudeBridge.cs      # SendPrompt() → process manager
│   ├── ProcessManager.cs    # claude -p (no --continue)
│   └── TranscriptWatcher.cs # JSONL parser
├── Voice/
│   ├── VoiceEngine.cs       # State machine + TTS + STT
│   ├── KeyboardWakeDetector.cs  # Win32 GetAsyncKeyState
│   ├── EdgeTTSEngine.cs     # Edge TTS (cute voice)
│   ├── WindowsSTTEngine.cs  # System.Speech STT
│   └── WindowsTTSEngine.cs  # Windows TTS fallback
├── UI/
│   ├── PetWindow.xaml/.cs   # Main window + all interaction wiring
│   ├── BubbleOverlay.xaml/.cs  # Chat popup
│   ├── StatusBubble.xaml/.cs   # Thought bubble above dog
│   ├── SettingsWindow.xaml/.cs # Settings panel
│   ├── TrayManager.cs       # System tray
│   └── HudMiniBar.xaml/.cs  # (deprecated, replaced by StatusBubble)
└── Animation/
    ├── SpriteSheetPlayer.cs # Image + programmatic transforms (scale/rotate/translate)
    └── SpriteGenerator.cs   # (unused — replaced by Image/千千.png)
```

## Debugging Memory Issues

When 千千 responds with wrong identity or mentions "旺财"/"Claude":

1. Check `D:\dev\claude-pet\last_prompt.txt` — the actual prompt sent to Claude
2. Check `memory/conversation/*.md` — old polluted entries
3. Check `memory/soul.md` — identity instructions
4. Check `memory/knowledge.md` — should NOT mention "Claude Code" or "旺财"
5. Clear polluted files: `rm memory/conversation/*.md`
6. Test directly: `claude -p "$(cat memory/soul.md)\n\n---\n[用户说] 你是谁？"`
