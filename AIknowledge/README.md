# Sandplay / sandtray knowledge library

Collected 2026-09-23 for therapist-oriented study and one grounded reflection assistant. **21 sources; 11 CC BY full-text articles; 21 bilingual reading notes; 15 notes eligible for runtime retrieval.** No paid books, purchases, advertisements or product pages were acquired as learning resources.

[Browse all 21 resources](SOURCE_INDEX.md).

## Start reading

1. [Method and terminology](notes/isst-method.md): distinguish Kalffian/Jungian sandplay from broader sandtray practices.
2. [Practice stance](notes/tsta-practice.md): client-owned meaning and open invitations.
3. [Documentation](notes/isst-case.md): construction process, exact words, context and change over time.
4. [Ethics](notes/isst-ethics.md) and [training](notes/isst-training.md).
5. [Conceptual comparison](fulltext/PMC11273505.md) and [longitudinal qualitative study](fulltext/PMC5415598.md).
6. [Evidence overview](notes/review-2026.md), [physical versus digital evidence](fulltext/PMC12929380.md), then the remaining studies with their limitations.

These materials support learning. They do not replace supervised clinical training. The notes were checked against sources by the coding assistant, **not reviewed or approved by a therapist**. English/Chinese notes are editorial paraphrases and translations, not official source translations. The research collection is selective, not an exhaustive systematic review.

## Files and attribution

- `catalog.json`: canonical source metadata and concise English/Chinese passages, locators, eligibility and review status.
- `notes/`: one human-readable note per source. Source links are retained.
- `fulltext/`: original CC BY JATS XML and readable Markdown extractions. XML preserves the original article, author attribution and license statement. Markdown omits some tables, figures and references; consult XML or the original for those details. Notes/extractions are adaptations, not publisher originals. Third-party material may have separate notices in the XML.
- `download_manifest.json`: author/title/DOI, license, original download URL, retrieval date and SHA-256 for each downloaded XML.
- [Exclusions](EXCLUSIONS.md): retractions, restricted reuse and unsuitable runtime studies.
- [Implementation and evaluation](IMPLEMENTATION.md).

## How the AI uses this

The existing Bailian assistant retrieves selected notes before answering. It does **not** automatically ingest the whole archive. The runtime library is a small curated corpus with bilingual lexical retrieval, foundation notes, evidence limits and server-owned citations. It is retrieval-augmented generation (RAG), not training new model weights. Downloading papers alone does not teach a model.

Full research articles often contain speculative interpretations, sensitive case descriptions or claims that need critical appraisal. Keeping the full archive for study while using selected method notes in reports makes the actual model context inspectable. Professional-source notes are brief original paraphrases; no license for the underlying website is asserted.

## Reproduce and maintain

From the Unity repository root:

```sh
python3 AIknowledge/tools/fetch_open_articles.py
python3 AIknowledge/tools/build_pack.py
```

Fetching is limited to an explicit CC BY allowlist, checks Europe PMC retraction flags and article licensing, and preserves original XML. It does not guarantee all future retractions have been indexed. Recheck source status before releases and review changes manually. A fetch failure leaves the existing runtime pack unchanged; do not treat partial downloads as a completed refresh.

To add knowledge: verify source and reuse terms; add a concise bilingual passage and evidence limitations to `catalog.json`; inspect the source; decide eligibility; regenerate the runtime pack; run retrieval and report tests. Do not insert client records or example case identities into this shared library. Clinician review should be recorded separately with reviewer/date, never inferred from successful software tests.

The generated pack lives in `api_backend/analysis/data/knowledge.json` because the backend is a separate repository and Docker build context. **Include changes in both repositories when releasing.** The backend image automatically includes this local file; no new external vector database or paid knowledge service is required. Bailian inference retains its normal provider costs.
