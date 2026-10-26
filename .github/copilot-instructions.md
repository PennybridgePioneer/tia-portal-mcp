# Copilot Instructions

## Projektrichtlinien
- Für den V20-Umbau ist fehlende API-Abdeckung gegenüber V21 erwartbar; die Datei Siemens.Engineering.Hmi.dll ist vorhanden, aber HMI-Themen haben aktuell keine hohe Priorität.
- Im aktuellen Deployment liegen die Worker-Binaries unter openness-worker\V20\net48 (und nicht direkt unter openness-worker\V20); der Serverpfad muss dieses Layout berücksichtigen.
- Auf dem Zielrechner ist nur TIA Portal V20 installiert; wenn der allgemeine Worker (V21) gestartet wird, crasht er wegen fehlender Siemens.Engineering.Base v21. Außerdem ist 6388 die TIA-Hauptinstanz und 10952 nur ein Such-/Ersetzungs-Hintergrundprozess.- Auf der aktuellen Maschine des Users ist TIA nicht installiert; Worker-Livetests sollen dort nicht durchgeführt werden.
- V20 soll vorerst mit reduziertem Funktionsumfang gebaut werden; CommunicationConnections-abhängige Features dürfen für V20 deaktiviert/fallbacked werden.