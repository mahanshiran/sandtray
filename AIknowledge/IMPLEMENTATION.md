# Implementation and verification

## Actual platform

This checkout uses Django/DRF (`api_backend/analysis/services.py`) with Bailian's compatible `/chat/completions` endpoint. Repository defaults are `qwen-plus` for text and `qwen3-vl-plus` for image requests. Environment settings can override them; these are not verified live production settings. The Unity client continues to use the same `/api/analysis/reflect/` endpoint and the same assistant.

## Request flow

1. Load the packaged knowledge corpus, check its schema and SHA-256, and cache it in process. Restart workers after changing the corpus.
2. Include two foundation notes about client-owned meaning and documentation. Rank other eligible notes with BM25 over English words and Chinese bigrams; supply up to five notes.
3. Use optional API field `reflection_focus` (maximum 1,000 characters) as the retrieval query. Existing Unity clients use a standard method query. Object names are not converted into diagnoses or used to retrieve condition-specific interpretations. There is no new focus input in the current Unity UI.
4. Send the selected English/Chinese notes, session data and optional screenshot to the same configured Bailian model. Session fields, images, notes and focus are explicitly treated as reference data, not instructions.
5. Request a JSON object containing a two-section standalone report and one to three selected passage IDs. Validate the schema, section ordering, output size and citation membership. Apply the existing interpretation restrictions, with an English word-boundary fix so “animals” no longer matches “anima”.
6. Retry once on invalid structure, citations or disallowed wording, then return the existing provider error. Missing/corrupted knowledge fails before the provider call; no silent ungrounded fallback.
7. Attach a short methodology statement and source titles/URLs on the server. The references support the **reporting method**, not a diagnosis or an asserted scientific explanation of this particular tray. Return `sources` and `knowledge_version` alongside existing `reflection`, `model`, `tokens_used` fields.
8. Show Scene composition, Practice context and Sources cards in Unity and preserve them in portable report text and PDF headings. These two UI sections are not editable through the report editor. Existing account metering and replay continue around this single provider operation.

Bailian JSON mode is documented at https://help.aliyun.com/zh/model-studio/qwen-structured-output (checked 2026-09-23). JSON Object mode does not guarantee a schema, which is why server validation is required. Non-thinking mode is selected for the existing default models. Custom model overrides must support that request contract.

## Why this first implementation

The runtime corpus contains 15 short, selected passages. Dependency-free local retrieval is easy to inspect and version at this size; it adds no embedding API, data transfer or vector-database service. The broader archive remains available for study and future expansion. No claim is made that lexical retrieval is the best approach for a much larger corpus: evaluate multilingual embeddings and reranking against real therapist questions before expanding substantially. Fine-tuning is not needed to make these sources available at request time.

Source validation prevents unknown IDs from being cited. It does **not** prove that generated language is factually correct or clinically appropriate. Prompt-injection resistance and interpretation filters are safeguards, not formal guarantees. There is no semantic entailment checker, no independent clinician review, no clinical validation, and no automatic access to a client's longitudinal history. Existing screenshots/session data are still sent to Bailian; deployment must follow the product's consent and privacy arrangements.

## Reproducible tests

From `api_backend`, with dependencies installed in a supported Python environment:

```sh
python manage.py test analysis.tests analysis.test_knowledge analysis.test_metering --settings=sandtray_api.test_settings
```

Tests cover bilingual retrieval, unknown queries, integrity failure, exclusions, malformed schema, invented/duplicate citations, ordered sections, provider retries, missing knowledge, JSON request configuration, source metadata, existing endpoint behavior and metering. Mock tests check request construction and failure behavior; they cannot establish how a live model handles an adversarial prompt.

For real-model evaluation, use synthetic fixtures only:

```sh
python AIknowledge/tools/evaluate_live.py --live --output /tmp/sandtray-live-evaluation.json
```

This explicitly makes provider calls and incurs normal API costs. It loads the backend configuration and requires `BAILIAN_API_KEY`. Review every generated answer against `evaluation_cases.json`, including Chinese fluency, empty scenes, water confusion, source relevance and prompt injection. Contract pass is not clinical pass. The fixtures do not test actual vision perception; add de-identified or synthetic screenshots before releasing a vision-model change. Compare model candidates on the same reviewed cases before changing the default.

## Local verification status

- Final production candidate: 41 tests run, 40 passed and one PostgreSQL-only concurrency test skipped on SQLite.
- Downloaded 11 CC BY XML originals and readable extractions; preserved attribution and checked checksums in the pack builder.
- No Bailian credential is configured in this local checkout. Production already had credentials; English, Chinese and vision provider smoke checks passed using synthetic data in the final candidate. Clinical evaluation has not been performed.
- Direct `dotnet build Assembly-CSharp.csproj --no-restore` is blocked by missing .NET Framework 4.7.1 reference assemblies. Verify the card display in the project's Unity editor before release.
- Backend updated 2026-09-23 as `783ef99a516beec0edc65424ccafb2f9aa4e6ef7`. See [compact-report deployment record](../docs/compact-report-deployment-2026-09-23.md). Unity UI changes still require the current workspace or a rebuilt device app.

## Standalone report revision

New reports use Observations and Conclusion, displayed as Key observations and Overall reading in the updated app. They do not ask questions or request user input. Scene composition synthesizes supported visible relationships; it does not speculate about motives or assign psychological meanings. Validation rejects questions, common invitations and leaked internal field names. The report remains complete when no personal narrative is supplied. Legacy headings remain recognized only for displaying archived reports.

## Concise report revision

The four narrative sections were consolidated into two to reduce repetition. A compact Method and sources appendix replaces the lengthy Practice context notes. Provider context still includes the selected knowledge notes; they are no longer copied into every user report. Validation checks common unsupported identity/size/grouping claims, raw measurements, text-only front/back claims, unsupported boundary claims and exact sentence repetition. Feedback reports all detected issues in a single rewrite request. These checks are limited lexical safeguards, not proof that every statement is correct.
