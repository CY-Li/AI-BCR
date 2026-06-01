# AGENTS.md

# AI-BCR Scanner Management System

Enterprise scanner management and AI OCR platform.

Current delivery strategy is **Prototype/Mock-first**:
- prioritize validating workflow stability and operability first
- preserve reliability and backward compatibility while iterating
- avoid large architectural rewrites before workflow proof is complete

This application is designed for:
- enterprise intranet environments
- unattended scanning workflows
- OCR automation
- ERP integration
- scanner fleet management

Reliability and workflow stability are more important than aggressive refactoring.

---

# Technology Stack

- C#
- .NET 8
- WPF (must-use UI framework)
- MVVM
- TWAIN/WIA
- OCR Service
- REST API
- Windows Desktop

UI framework policy:
- WPF is the only target UI architecture for ongoing/new development.
- WinUI 3 code currently in this repository is transitional prototype code only, not the target architecture baseline.

---

# Core Architecture

The application follows MVVM architecture.

Rules:
- ViewModels must not directly manipulate UI controls
- Business logic belongs in Services
- Avoid code-behind unless absolutely necessary
- Prefer dependency injection
- Use async/await consistently
- New features and refactoring must preserve WPF/MVVM migration compatibility for backend handoff.

Transitional status note:
- The current repository still contains WinUI 3 prototype implementation.
- Implementation decisions and future direction must follow this AGENTS.md policy (WPF + MVVM as target baseline).

Folder responsibilities:

- Views/
  UI only

- ViewModels/
  UI state and interaction logic

- Services/
  hardware, OCR, API, scanner, networking

- Models/
  data structures

- Helpers/
  utility functions only

---

# Scanner Workflow

Main workflow:

1. Detect paper sensor
2. Wait for stable state
3. Trigger scan
4. Image preprocessing
5. OCR processing
6. JSON normalization
7. Upload to ERP
8. Return to idle state

Important:
- prevent duplicate triggers
- debounce sensor events
- scanner disconnects must not crash workflow
- temporary sensor glitches must be tolerated

---

# Auto Scan Rules

Auto Scan is the highest priority feature.

Requirements:
- fault tolerant
- self recovering
- non-blocking
- stable under hardware instability

Never:
- block UI thread
- allow recursive scan loops
- trigger duplicate scans
- terminate workflow on temporary scanner errors

Always:
- use try/catch for hardware events
- log failures safely
- restore idle state after failures
- support scanner reconnect

---

# OCR Pipeline Rules

OCR pipeline:

1. image capture
2. edge detection
3. perspective correction
4. OCR extraction
5. AI normalization

Requirements:
- OCR schema compatibility is critical
- preserve backward compatibility
- OCR failures should not crash scanner workflow

Do not:
- modify OCR JSON schema unless explicitly requested
- hardcode OCR endpoints
- assume network stability

---

# UIUX Guidelines

This application is workflow-oriented.

UI priorities:
- clarity
- operator confidence
- workflow visibility
- responsiveness
- reliability feeling

Users must always understand:
- current scan state
- OCR progress
- upload status
- scanner connectivity
- current errors

---

# UI Layout Philosophy

Prefer:
- large status areas
- workflow-oriented UI
- dashboard style layouts
- minimal operator confusion
- modern industrial UI

Avoid:
- engineering-heavy screens
- excessive debug information
- crowded layouts
- excessive modal dialogs
- WinForms-style UI

The UI should feel:
- modern
- stable
- responsive
- reliable
- industrial-grade

---

# Main Status Design

The UI should clearly display states such as:

- READY
- DETECTING PAPER
- SCANNING
- OCR PROCESSING
- UPLOADING
- SUCCESS
- ERROR

Status visibility is critical.

Users should never wonder:
- whether scanning is active
- whether OCR is stuck
- whether upload failed
- whether the scanner disconnected

---

# Debug Information Rules

Debug information should be separated from operator UI.

Debug panels should:
- be collapsible
- not dominate the main screen
- not confuse operators

Avoid exposing:
- raw sensor spam
- stack traces
- engineering logs
- internal exceptions

on the primary operator interface.

---

# Performance Rules

The application must remain responsive.

Requirements:
- never block UI thread
- avoid synchronous waits
- avoid long operations in UI events
- use cancellation tokens where possible
- prefer background processing

---

# Error Handling Rules

Hardware instability is expected.

Always assume:
- scanner disconnect
- network timeout
- OCR timeout
- duplicate hardware events
- sensor glitches

Requirements:
- never crash application
- fail gracefully
- auto recover when possible
- preserve workflow continuity

Never allow:
- async void crashes
- unhandled hardware exceptions
- UI freeze during scan
- application termination from sensor events

---

# Critical Files

These files are sensitive:

- MainWindow.xaml.cs
- ScannerService.cs
- TwainService.cs
- OCRService.cs
- AutoScanService.cs

Before major refactoring:
- explain reasoning first
- preserve backward compatibility
- avoid unnecessary architectural rewrites

---

# Code Style

Use:
- PascalCase
- nullable reference types
- explicit async/await
- dependency injection
- small focused methods

Avoid:
- magic numbers
- giant methods
- deep nested try/catch
- duplicated logic
- blocking calls

---

# Networking Rules

The application operates inside enterprise intranet environments.

Requirements:
- tolerate unstable DNS
- tolerate temporary network loss
- avoid hardcoded IP addresses
- support reconnect behavior
- support offline recovery

Never assume:
- stable internet
- stable DNS
- low latency

---

# Build Verification

Before completing modifications:

```bash
dotnet build
