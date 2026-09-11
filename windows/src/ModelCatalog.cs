// Curated GGUF models for the embedded engine.
//
// Small instruct models with a direct Hugging Face GGUF download, mirroring
// what the phone build can run. Sizes are the published Q4_K_M (or Q8 for the
// tiny SmolLM) downloads; the downloader asks the server for the real length
// before fetching and falls back to these when the server does not answer.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal sealed class ModelEntry
    {
        public string Name;
        public string Repo;
        public string File;
        public long Bytes;
        public string Note;

        public string DisplaySize
        {
            get
            {
                double mb = Bytes / (1024.0 * 1024.0);
                return mb >= 1024 ? mb.ToString("0.0") + " GB" : mb.ToString("0") + " MB";
            }
        }
    }

    internal static class ModelCatalog
    {
        public static readonly List<ModelEntry> All = new List<ModelEntry>
        {
            new ModelEntry
            {
                Name = "SmolLM2-135M",
                Repo = "bartowski/SmolLM2-135M-Instruct-GGUF",
                File = "SmolLM2-135M-Instruct-Q4_K_M.gguf",
                Bytes = 81L * 1024 * 1024,
                Note = "Ultra light. Great for clipboard formatting.",
            },
            new ModelEntry
            {
                Name = "Qwen2.5-0.5B",
                Repo = "Qwen/Qwen2.5-0.5B-Instruct-GGUF",
                File = "qwen2.5-0.5b-instruct-q4_k_m.gguf",
                Bytes = 406L * 1024 * 1024,
                Note = "Fast multilingual general model.",
            },
            new ModelEntry
            {
                Name = "Llama-3.2-1B",
                Repo = "unsloth/Llama-3.2-1B-Instruct-GGUF",
                File = "Llama-3.2-1B-Instruct-Q4_K_M.gguf",
                Bytes = 749L * 1024 * 1024,
                Note = "Meta's small instruct model.",
            },
            new ModelEntry
            {
                Name = "Qwen2.5-Coder-3B",
                Repo = "unsloth/Qwen2.5-Coder-3B-Instruct-GGUF",
                File = "Qwen2.5-Coder-3B-Instruct-Q4_K_M.gguf",
                Bytes = 1982L * 1024 * 1024,
                Note = "Code focused. Strong at rewriting text too.",
            },
            new ModelEntry
            {
                Name = "Llama-3.2-3B",
                Repo = "unsloth/Llama-3.2-3B-Instruct-GGUF",
                File = "Llama-3.2-3B-Instruct-Q4_K_M.gguf",
                Bytes = 2015L * 1024 * 1024,
                Note = "Balanced speed and quality.",
            },
            new ModelEntry
            {
                Name = "Gemma-2-2B",
                Repo = "bartowski/gemma-2-2b-it-GGUF",
                File = "gemma-2-2b-it-Q4_K_M.gguf",
                Bytes = 1727L * 1024 * 1024,
                Note = "Google's efficient model. Strong for its size.",
            },
            new ModelEntry
            {
                Name = "Phi-3.5-Mini",
                Repo = "bartowski/Phi-3.5-mini-instruct-GGUF",
                File = "Phi-3.5-mini-instruct-Q4_K_M.gguf",
                Bytes = 2309L * 1024 * 1024,
                Note = "Microsoft's compact but capable model.",
            },
        };

        public static string DownloadUrl(ModelEntry m)
        {
            return "https://huggingface.co/" + m.Repo + "/resolve/main/" + m.File;
        }
    }
}
