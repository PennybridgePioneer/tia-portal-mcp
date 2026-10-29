# GitHub Copilot Instructions

## Solution Context
This solution contains projects targeting .NET 10 and .NET Framework 4.8.

## Framework Compatibility
- Always inspect the target framework of the project before generating or modifying code.
- Never use .NET 10-only APIs in .NET Framework 4.8 projects.
- Respect each project's C# language version.
- Do not assume that all projects use SDK-style project files.
- Preserve existing packages.config, app.config and binding redirects where applicable.
- Do not introduce nullable reference types, records, or newer C# syntax into legacy projects unless explicitly supported.

## Architecture
- Preserve existing public APIs and binary compatibility unless explicitly requested otherwise.
- Prefer minimal, targeted changes.
- Avoid unnecessary dependencies and architectural rewrites.
- For shared libraries, verify compatibility with every consuming target framework.
- Do not assume .NET 10 and .NET Framework 4.8 can directly reference each other's application assemblies.
- Consider .NET Standard 2.0 for shared libraries when appropriate.

## Coding
- Follow existing naming conventions and code style.
- Use async/await only where supported and appropriate.
- Avoid introducing deadlocks or blocking async operations.
- Do not replace existing framework-specific implementations without a clear reason.

## Validation
- Identify affected projects before editing.
- Build and test both target frameworks where applicable.
- Explain compatibility risks.
- Summarize changed files and potential breaking changes.
- Never perform automatic framework upgrades without approval.

## Response Language
- Explain changes in German.
- Write code and technical identifiers in English.

## Projektrichtlinien
- Für den V20-Umbau ist fehlende API-Abdeckung gegenüber V21 erwartbar; die Datei Siemens.Engineering.Hmi.dll ist vorhanden, aber HMI-Themen haben aktuell keine hohe Priorität.
- Im aktuellen Deployment liegen die Worker-Binaries unter openness-worker\V20\net48 (und nicht direkt unter openness-worker\V20); der Serverpfad muss dieses Layout berücksichtigen.
- Auf dem Zielrechner ist nur TIA Portal V20 installiert; wenn der allgemeine Worker (V21) gestartet wird, crasht er wegen fehlender Siemens.Engineering.Base v21. Außerdem ist 6388 die TIA-Hauptinstanz und 10952 nur ein Such-/Ersetzungs-Hintergrundprozess.
- Auf der aktuellen Maschine des Users ist TIA nicht installiert; Worker-Livetests sollen dort nicht durchgeführt werden.
- V20 soll vorerst mit reduziertem Funktionsumfang gebaut werden; CommunicationConnections-abhängige Features dürfen für V20 deaktiviert/fallbacked werden.
