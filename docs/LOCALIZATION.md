# Interface languages

English and Simplified Chinese retain persisted IDs 0 and 1. Added Spanish (2), Brazilian Portuguese (3), French (4), German (5), and Japanese (6).

Choose a language under Settings → Language. Preferences are saved on the device; the device language is used on first launch. Dates and numeric UI values use the selected locale. Stored identifiers, ISO dates, board names and client names are not translated.

`Assets/Resources/Localization/AdditionalLanguages.txt` is UTF-8. Each row has six pipe-separated columns: key, Spanish, Brazilian Portuguese, French, German, Japanese. Use literal `\n` for line breaks and preserve format placeholders such as `{0:F1}`. Language-neutral product names and symbols are mapped explicitly in `Localization.Languages.cs`.

All 407 current English localization keys have corresponding entries or explicit neutral values in the five new languages. Older library labels are handled through `Localization.Text`. Text that remains hardcoded outside this system, uploaded asset names, server error messages and existing report contents are not automatically translated.

The reflection API currently accepts only English and Chinese. The new interface languages continue requesting English reflections to remain compatible with the deployed API. Multilingual generated reflections require a separate backend update, including translated section validation and output checks.

Translations are a first editorial pass, not certified clinical translations. A native-speaking therapist should review terminology and disclaimers before public release.

Japanese uses Noto Sans CJK JP, from https://github.com/notofonts/noto-cjk, with its SIL Open Font License in `Assets/Resources/Fonts/NotoSansCJKjp-LICENSE.txt`.

Tests: `LocalizationLanguageTests` checks completeness, formatting placeholders, persisted IDs, locale selection and Japanese font glyph coverage. `SettingsRowsTests` checks the language dropdown and settings entry points.
