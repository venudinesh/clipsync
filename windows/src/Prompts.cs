// The prompts. Kept in one place and byte identical to the phone build, because
// a model answers a rephrased instruction differently and a clip processed on
// the desktop should come back looking like one processed on the phone.

using System;

namespace ClipSyncAI
{
    internal static class Prompts
    {
        public const string System =
            "You are a clipboard text processor. Convert raw copied text into clean, structured Markdown.";

        public static string Build(string rawText)
        {
            return
                "Convert the following raw copied text into clean, structured Markdown.\n" +
                "Rules:\n" +
                "- Use bullet points or numbered lists where appropriate\n" +
                "- Detect and preserve URLs as clickable markdown links\n" +
                "- Detect dates and format them clearly\n" +
                "- Detect email addresses and format as mailto links\n" +
                "- If the text looks like code, wrap it in code blocks\n" +
                "- If it contains checkboxes, convert to markdown checkboxes\n" +
                "- Keep the original meaning, don't add content\n" +
                "- Output ONLY the formatted markdown, no explanations\n" +
                "\n" +
                "Raw text:\n" + rawText;
        }

        /// Generation settings for the clipboard pass. Low temperature because
        /// reformatting is not a place for invention.
        public const int ProcessMaxTokens = 512;
        public const double ProcessTemperature = 0.3;
        public const double ProcessTopP = 0.9;

        /// Slightly warmer and longer for a free instruction the user typed.
        public const int InstructMaxTokens = 640;
        public const double InstructTemperature = 0.4;
        public const double InstructTopP = 0.9;

        /// The chat system prompt, the phone's wording with one word changed:
        /// "phone" became "PC", because that sentence states where the model is
        /// running and on this build the answer is different. Everything else is
        /// left alone for the same reason the note prompts are.
        public const string Chat =
            "You are ClipSync AI, a helpful on-device assistant. You run entirely locally " +
            "on the user's PC. Be concise, helpful, and friendly. Format responses with " +
            "markdown when appropriate. When the user shares documents or images, analyze " +
            "the extracted text and provide useful insights.";

        /// A conversation is allowed a longer answer than a clipboard reformat,
        /// and its temperature comes from the tone control rather than from here.
        public const int ChatMaxTokens = 1024;
        public const double ChatTopP = 0.9;

        /// What an attachment is worth reading. Past this the model spends its
        /// context on the tail of a document instead of the question about it.
        public const int AttachmentLimit = 5000;

        public const string ChatSummaryPrefix =
            "Summarize the conversation below. Give a short paragraph of what was discussed, " +
            "then bullet any decisions or answers that were reached. Keep names, numbers and " +
            "code exact.\n\n";

        public const string ChatMessagePrefix =
            "Summarize the following in three short bullet points, keeping any numbers or " +
            "names exact:\n\n";

        public const string SummarisePrefix = "Summarise the following text in three short bullet points. Output only the bullets.\n\n";
        public const string ExplainPrefix = "Explain the following text in plain language, briefly.\n\n";
        public const string RewritePrefix = "Rewrite the following text to be clearer and shorter. Keep the meaning. Output only the rewrite.\n\n";
        public const string TranslatePrefix = "Translate the following text into English. Output only the translation.\n\n";
        public const string KeyPointsPrefix = "List the key points in the following text as a markdown list. Output only the list.\n\n";
        public const string OcrCleanPrefix = "The following text came from optical character recognition and may contain errors. Fix obvious mistakes and lay it out as clean Markdown. Output only the corrected text.\n\n";

        /// The note prompts. These read "Summarize" where the buttons above them
        /// read "Summarise", and that is deliberate: the label is ours to spell
        /// the way the rest of the app spells, and the instruction is the phone's
        /// to the letter, because a model answers a reworded prompt differently.
        public const string NoteSummaryPrefix =
            "Summarize the note below in three short bullet points. Keep names, numbers and dates exact. Output only the bullets.\n\n";
        public const string NoteTidyPrefix =
            "Summarize the note below concisely, as markdown. Keep names, numbers and dates exact. Output only the summary.\n\n";
        public const string NoteTasksPrefix =
            "Extract every action item from the note below as a markdown checklist. Output only the checklist.\n\n";
        public const string NoteExpandPrefix =
            "Expand the note below with more detail and clearer structure, as markdown. Keep the original meaning and every fact that is already there. Output only the note.\n\n";
        public const string NoteGrammarPrefix =
            "Correct the spelling, grammar and punctuation of the text below. Keep the wording, the meaning, the language and the markdown formatting. Output only the corrected text.\n\n";
    }
}
