# AI report behavior

The backend uses the existing Bailian text/vision models and local bilingual knowledge retrieval. Knowledge stays internal: new model requests ask for only `reflection`, and generated reports have no reference or methodology appendix. The API retains an empty `sources` array for compatibility. Legacy passage-ID responses are validated if a provider supplies them, but their references are never appended.

## Report content

- **Observations:** one to three short, supported facts about the scene.
- **Conclusion:** for sufficiently detailed scenes, up to two tentative emotional or relational themes, tied to independent visible relationships, with a brief plausible alternative. These describe possible stories in the scene, not the creator's actual emotions or stable personality.
- No questions, requests for answers, diagnosis, treatment advice, personality typing, fixed symbol dictionary, or universal direction/color meanings.
- Empty and single-object scenes without distinct terrain receive a short deterministic insufficient-detail response. They do not invoke the model or invent significance for a lone hamburger. This still counts as an analysis request under the existing access policy.
- Richer scenes are not automatically psychologically informative. The model is instructed to remain brief when supported relationships are limited.

## Terrain

The app generates random uneven sand, including hills. The legacy `HasMeaningfulRelief` field indicates height variation only. It does not establish user action or psychological importance.

New clients flag substantial terrain outside the default generator's theoretical height envelope, requiring more than isolated samples. This is a conservative signal, not an edit-history comparison: edits within that envelope may be missed. The prompt allows clearly distinctive image-supported formations to be described, while omitting ordinary unevenness. No claim of deliberate sculpting is justified without process evidence. Blue excavated patches expose the tray base; blue walls are the perimeter, and neither automatically means water.

## Presentation and verification

Unity hides legacy reference sections from report cards and removes them before PDF requests. The backend also removes them when rendering PDFs, preserving saved originals.

Tests cover sparse scenes, bilingual boundaries, legacy cleanup, PDF output, schema validation and terrain thresholds. Live synthetic provider checks are recorded separately. Wording checks are limited safeguards; they do not establish factual or clinical validity. No claim of clinical validation is made.
