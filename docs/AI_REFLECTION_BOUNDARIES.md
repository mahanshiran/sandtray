# AI-assisted reflection boundaries

AI-assisted reflection describes the visible session material. It is not a diagnosis, clinical conclusion, treatment recommendation, risk assessment, or claim about the creator's inner state.

The backend requires three sections:

1. **Observations** — visible or supplied facts only.
2. **Optional hypotheses** — clearly tentative questions about a visible arrangement choice, always with a practical, non-symbolic alternative.
3. **Questions for reflection** — open questions that leave personal meaning to the creator.

The safety prompt and response validator reject diagnoses, trauma or mental-health inferences, universal symbolism, archetypes, and inferred motives or emotions. They also reject inferential wording inside the observations section. An invalid provider response is rewritten once; if it remains invalid, the request fails instead of returning the unsafe draft.

The app labels AI material as a draft for human review. It keeps AI reflection separate from observed events, the client's own explanation, practitioner notes, and agreed next steps. Sharing defaults exclude private practitioner notes and unreviewed AI text.

## Verification

`analysis` backend tests cover authentication, prompt construction, unsafe symbolic output rewrite, and inferred-observation rewrite. Unity report tests cover the distinct saved sections, template snapshots, author-only editing, and sharing exclusion of private/unreviewed fields.

These guardrails reduce inappropriate generated content. They do not establish clinical validation, professional licensure, or a guarantee about every third-party model response.
