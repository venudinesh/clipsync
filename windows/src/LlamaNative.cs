// Bindings to llama.h for the pinned b10809 runtime.
//
// Struct layouts follow the header byte for byte (pointers 8 bytes on x64,
// C bools one byte, size_t platform width). The default-params exports fill
// them correctly; the engine only adjusts a handful of integer fields. All
// entry points are resolved dynamically so the runtime version is checked at
// load time, not at application start.

using System;
using System.Runtime.InteropServices;

namespace ClipSyncAI
{
    // llama_model_params (CPU use: only n_gpu_layers is ever set to 0).
    //
    // The C bools are declared as bytes, not marshaled bools: these structs
    // are returned by value from the DLL, and Marshal.GetDelegateForFunction
    // Pointer cannot marshal a non-blittable struct return. A byte is the same
    // one byte a C _Bool occupies, so the layout is unchanged.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaModelParams
    {
        public IntPtr Devices;
        public IntPtr TensorBuftOverrides;
        public int NGpuLayers;
        public int SplitMode;
        public int LoadMode;
        public int LazyMode;
        public int MainGpu;
        public IntPtr TensorSplit;
        public IntPtr ProgressCallback;
        public IntPtr ProgressCallbackUserData;
        public IntPtr KvOverrides;
        public byte VocabOnly;
        public byte CheckTensors;
        public byte UseExtraBufts;
        public byte NoHost;
        public byte NoAlloc;
        public byte LoadMtp;
    }

    // llama_context_params.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaContextParams
    {
        public uint NCtx;
        public uint NBatch;
        public uint NUbatch;
        public uint NSeqMax;
        public uint NRsSeq;
        public uint NOutputsMax;
        public uint NOutputsMaxPerSeq;
        public int NThreads;
        public int NThreadsBatch;
        public int CtxType;
        public int RopeScalingType;
        public int PoolingType;
        public int AttentionType;
        public int FlashAttnType;
        public float RopeFreqBase;
        public float RopeFreqScale;
        public float YarnExtFactor;
        public float YarnAttnFactor;
        public float YarnBetaFast;
        public float YarnBetaSlow;
        public uint YarnOrigCtx;
        public float DefragThold;
        public IntPtr CbEval;
        public IntPtr CbEvalUserData;
        public int TypeK;
        public int TypeV;
        public IntPtr AbortCallback;
        public IntPtr AbortCallbackData;
        public byte Embeddings;
        public byte OffloadKqv;
        public byte NoPerf;
        public byte OpOffload;
        public byte SwaFull;
        public byte KvUnified;
        public IntPtr Samplers;
        public IntPtr NSamplers;
        public IntPtr CtxOther;
    }

    // llama_batch.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaBatch
    {
        public int NTokens;
        public IntPtr Token;
        public IntPtr Embd;
        public IntPtr Pos;
        public IntPtr NSeqId;
        public IntPtr SeqId;
        public IntPtr Logits;
    }

    // llama_sampler_chain_params.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaSamplerChainParams
    {
        public byte NoPerf;
    }

    // llama_chat_message.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaChatMessage
    {
        public IntPtr Role;
        public IntPtr Content;
    }

    // One delegate per export the embedded engine touches.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaBackendInit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaBackendFree();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GgmlBackendLoadAllFromPath(IntPtr dirPathUtf8);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LlamaModelParams LlamaModelDefaultParams();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaModelLoadFromFile(IntPtr pathUtf8, LlamaModelParams p);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaModelFree(IntPtr model);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaModelGetVocab(IntPtr model);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaModelChatTemplate(IntPtr model, IntPtr nameUtf8);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LlamaContextParams LlamaContextDefaultParams();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaInitFromModel(IntPtr model, LlamaContextParams p);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaFree(IntPtr ctx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaTokenize(IntPtr vocab, IntPtr textUtf8, int textLen,
        IntPtr tokens, int maxTokens, [MarshalAs(UnmanagedType.I1)] bool addSpecial,
        [MarshalAs(UnmanagedType.I1)] bool parseSpecial);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaTokenToPiece(IntPtr vocab, int token, IntPtr buf,
        int length, int lstrip, [MarshalAs(UnmanagedType.I1)] bool special);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LlamaBatch LlamaBatchGetOne(IntPtr tokens, int n);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaBatchFree(LlamaBatch b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaDecode(IntPtr ctx, LlamaBatch b);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LlamaSamplerChainParams LlamaSamplerChainDefaultParams();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaSamplerChainInit(LlamaSamplerChainParams p);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaSamplerChainAdd(IntPtr chain, IntPtr sampler);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaSamplerInitGreedy();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaSamplerInitDist(uint seed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaSamplerInitTopP(float p, IntPtr minKeep);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaSamplerInitTemp(float t);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaSamplerFree(IntPtr sampler);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaSamplerSample(IntPtr chain, IntPtr ctx, int idx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal delegate bool LlamaVocabIsEog(IntPtr vocab, int token);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaVocabEos(IntPtr vocab);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int LlamaChatApplyTemplate(string tmpl,
        IntPtr chat, IntPtr nMsg, [MarshalAs(UnmanagedType.I1)] bool addAss,
        IntPtr buf, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr LlamaGetMemory(IntPtr ctx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void LlamaMemoryClear(IntPtr mem, [MarshalAs(UnmanagedType.I1)] bool data);

    /// Entry points, bound the first time one of them is used.
    ///
    /// They are deliberately not static readonly initializers: a type whose
    /// static fields carry initializers is beforefieldinit, so the runtime may
    /// run its constructor at any moment — including before the loader has put
    /// the runtime on disk — and a failure there is cached forever. Binding on
    /// demand after [NativeLoader.Available] has run means the first use of an
    /// embedded model is the only moment the runtime is ever asked to load.
    internal static class Llama
    {
        private static bool _bound;

        private static void Ensure()
        {
            if (_bound) return;
            if (!NativeLoader.Available)
            {
                throw new InvalidOperationException(
                    NativeLoader.Error.Length == 0
                        ? "The bundled model runtime is not available."
                        : NativeLoader.Error);
            }
            _backendInit = NativeLoader.Bind<LlamaBackendInit>("llama_backend_init");
            _backendFree = NativeLoader.Bind<LlamaBackendFree>("llama_backend_free");
            _backendLoadAll = NativeLoader.BindFrom<GgmlBackendLoadAllFromPath>("ggml.dll", "ggml_backend_load_all_from_path");
            _modelDefaultParams = NativeLoader.Bind<LlamaModelDefaultParams>("llama_model_default_params");
            _modelLoadFromFile = NativeLoader.Bind<LlamaModelLoadFromFile>("llama_model_load_from_file");
            _modelFree = NativeLoader.Bind<LlamaModelFree>("llama_model_free");
            _modelGetVocab = NativeLoader.Bind<LlamaModelGetVocab>("llama_model_get_vocab");
            _modelChatTemplate = NativeLoader.Bind<LlamaModelChatTemplate>("llama_model_chat_template");
            _contextDefaultParams = NativeLoader.Bind<LlamaContextDefaultParams>("llama_context_default_params");
            _initFromModel = NativeLoader.Bind<LlamaInitFromModel>("llama_init_from_model");
            _free = NativeLoader.Bind<LlamaFree>("llama_free");
            _tokenize = NativeLoader.Bind<LlamaTokenize>("llama_tokenize");
            _tokenToPiece = NativeLoader.Bind<LlamaTokenToPiece>("llama_token_to_piece");
            _batchGetOne = NativeLoader.Bind<LlamaBatchGetOne>("llama_batch_get_one");
            _batchFree = NativeLoader.Bind<LlamaBatchFree>("llama_batch_free");
            _decode = NativeLoader.Bind<LlamaDecode>("llama_decode");
            _samplerChainDefaultParams = NativeLoader.Bind<LlamaSamplerChainDefaultParams>("llama_sampler_chain_default_params");
            _samplerChainInit = NativeLoader.Bind<LlamaSamplerChainInit>("llama_sampler_chain_init");
            _samplerChainAdd = NativeLoader.Bind<LlamaSamplerChainAdd>("llama_sampler_chain_add");
            _samplerInitGreedy = NativeLoader.Bind<LlamaSamplerInitGreedy>("llama_sampler_init_greedy");
            _samplerInitDist = NativeLoader.Bind<LlamaSamplerInitDist>("llama_sampler_init_dist");
            _samplerInitTopP = NativeLoader.Bind<LlamaSamplerInitTopP>("llama_sampler_init_top_p");
            _samplerInitTemp = NativeLoader.Bind<LlamaSamplerInitTemp>("llama_sampler_init_temp");
            _samplerFree = NativeLoader.Bind<LlamaSamplerFree>("llama_sampler_free");
            _samplerSample = NativeLoader.Bind<LlamaSamplerSample>("llama_sampler_sample");
            _vocabIsEog = NativeLoader.Bind<LlamaVocabIsEog>("llama_vocab_is_eog");
            _vocabEos = NativeLoader.Bind<LlamaVocabEos>("llama_vocab_eos");
            _chatApplyTemplate = NativeLoader.Bind<LlamaChatApplyTemplate>("llama_chat_apply_template");
            _getMemory = NativeLoader.Bind<LlamaGetMemory>("llama_get_memory");
            _memoryClear = NativeLoader.Bind<LlamaMemoryClear>("llama_memory_clear");
            _bound = true;
        }

        public static LlamaBackendInit BackendInit { get { Ensure(); return _backendInit; } }
        public static LlamaBackendFree BackendFree { get { Ensure(); return _backendFree; } }
        public static GgmlBackendLoadAllFromPath BackendLoadAllFromPath { get { Ensure(); return _backendLoadAll; } }
        public static LlamaModelDefaultParams ModelDefaultParams { get { Ensure(); return _modelDefaultParams; } }
        public static LlamaModelLoadFromFile ModelLoadFromFile { get { Ensure(); return _modelLoadFromFile; } }
        public static LlamaModelFree ModelFree { get { Ensure(); return _modelFree; } }
        public static LlamaModelGetVocab ModelGetVocab { get { Ensure(); return _modelGetVocab; } }
        public static LlamaModelChatTemplate ModelChatTemplate { get { Ensure(); return _modelChatTemplate; } }
        public static LlamaContextDefaultParams ContextDefaultParams { get { Ensure(); return _contextDefaultParams; } }
        public static LlamaInitFromModel InitFromModel { get { Ensure(); return _initFromModel; } }
        public static LlamaFree Free { get { Ensure(); return _free; } }
        public static LlamaTokenize Tokenize { get { Ensure(); return _tokenize; } }
        public static LlamaTokenToPiece TokenToPiece { get { Ensure(); return _tokenToPiece; } }
        public static LlamaBatchGetOne BatchGetOne { get { Ensure(); return _batchGetOne; } }
        public static LlamaBatchFree BatchFree { get { Ensure(); return _batchFree; } }
        public static LlamaDecode Decode { get { Ensure(); return _decode; } }
        public static LlamaSamplerChainDefaultParams SamplerChainDefaultParams { get { Ensure(); return _samplerChainDefaultParams; } }
        public static LlamaSamplerChainInit SamplerChainInit { get { Ensure(); return _samplerChainInit; } }
        public static LlamaSamplerChainAdd SamplerChainAdd { get { Ensure(); return _samplerChainAdd; } }
        public static LlamaSamplerInitGreedy SamplerInitGreedy { get { Ensure(); return _samplerInitGreedy; } }
        public static LlamaSamplerInitDist SamplerInitDist { get { Ensure(); return _samplerInitDist; } }
        public static LlamaSamplerInitTopP SamplerInitTopP { get { Ensure(); return _samplerInitTopP; } }
        public static LlamaSamplerInitTemp SamplerInitTemp { get { Ensure(); return _samplerInitTemp; } }
        public static LlamaSamplerFree SamplerFree { get { Ensure(); return _samplerFree; } }
        public static LlamaSamplerSample SamplerSample { get { Ensure(); return _samplerSample; } }
        public static LlamaVocabIsEog VocabIsEog { get { Ensure(); return _vocabIsEog; } }
        public static LlamaVocabEos VocabEos { get { Ensure(); return _vocabEos; } }
        public static LlamaChatApplyTemplate ChatApplyTemplate { get { Ensure(); return _chatApplyTemplate; } }
        public static LlamaGetMemory GetMemory { get { Ensure(); return _getMemory; } }
        public static LlamaMemoryClear MemoryClear { get { Ensure(); return _memoryClear; } }

        private static LlamaBackendInit _backendInit;
        private static LlamaBackendFree _backendFree;
        private static GgmlBackendLoadAllFromPath _backendLoadAll;
        private static LlamaModelDefaultParams _modelDefaultParams;
        private static LlamaModelLoadFromFile _modelLoadFromFile;
        private static LlamaModelFree _modelFree;
        private static LlamaModelGetVocab _modelGetVocab;
        private static LlamaModelChatTemplate _modelChatTemplate;
        private static LlamaContextDefaultParams _contextDefaultParams;
        private static LlamaInitFromModel _initFromModel;
        private static LlamaFree _free;
        private static LlamaTokenize _tokenize;
        private static LlamaTokenToPiece _tokenToPiece;
        private static LlamaBatchGetOne _batchGetOne;
        private static LlamaBatchFree _batchFree;
        private static LlamaDecode _decode;
        private static LlamaSamplerChainDefaultParams _samplerChainDefaultParams;
        private static LlamaSamplerChainInit _samplerChainInit;
        private static LlamaSamplerChainAdd _samplerChainAdd;
        private static LlamaSamplerInitGreedy _samplerInitGreedy;
        private static LlamaSamplerInitDist _samplerInitDist;
        private static LlamaSamplerInitTopP _samplerInitTopP;
        private static LlamaSamplerInitTemp _samplerInitTemp;
        private static LlamaSamplerFree _samplerFree;
        private static LlamaSamplerSample _samplerSample;
        private static LlamaVocabIsEog _vocabIsEog;
        private static LlamaVocabEos _vocabEos;
        private static LlamaChatApplyTemplate _chatApplyTemplate;
        private static LlamaGetMemory _getMemory;
        private static LlamaMemoryClear _memoryClear;
    }
}
