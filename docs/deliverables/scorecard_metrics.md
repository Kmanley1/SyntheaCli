# Scorecard Metrics

The repository root includes `scorecard.json`, which captures baseline architecture scores. These metrics track structure, design qualities, dependencies, pipeline, and documentation.

```json
{
  "overall": "B",
  "criteria": {
    "structure":        { "score": 4, "comment": "Simple layout with tests; limited layering." },
    "designQualities":  { "score": 3, "comment": "Modular but Program.cs could grow." },
    "crossCutting":     { "score": 3, "comment": "Caching works but lacks structured logging." },
    "dependencies":     { "score": 2, "comment": "Relies on GitHub for JAR and beta CLI package." },
    "pipeline":         { "score": 4, "comment": "Setup script and tests integrated; Dockerfile missing." },
    "documentation":    { "score": 4, "comment": "README detailed but architecture docs minimal." }
  }
}
```

The file can be consumed by dashboards or scripts to monitor progress over time.

[Download scorecard.json](../../scorecard.json)
