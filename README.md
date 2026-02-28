# Advanced Addressables + Live-Ops Content Pipeline (Unity 2023+)

### Project Highlights
- Remote DLC loader with **local fallback** (offline resilience)
- Dynamic bundle patching via **Unity CCD** (no app resubmit)
- Isolated **A/B variant testing** (label-based, runtime switchable)
- Dependency-safe instantiation (`InstantiateAsync`) → no mesh/material missing
- Real-time progress UI, load-time & memory metrics
- Object pooling, graceful error handling, retries & timeouts
- Fully configuration-driven via ScriptableObject

### Technologies & Best Practices
- Addressables + CCD (remote content delivery)
- Async/await + `InstantiateAsync` (performance & reliability)
- ObjectPool (GC-friendly spawning)
- ScriptableObject config (designer-friendly, hot-swappable)

### Why is this essential?
Live-service games demand small app sizes, frequent patches, data-driven A/B testing, and mobile/offline support.  
This prototype demonstrates exactly that: a production-ready content pipeline built for real-world live-ops constraints.
