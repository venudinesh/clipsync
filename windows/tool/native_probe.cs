using System;
class Probe
{
    static void Try<T>(string name) where T : class
    {
        try
        {
            var d = ClipSyncAI.NativeLoader.Bind<T>(name);
            Console.WriteLine("  ok   " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL " + name + " :: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    static void Main()
    {
        Console.WriteLine("available: " + ClipSyncAI.NativeLoader.Available);
        Try<ClipSyncAI.LlamaBackendInit>("llama_backend_init");
        Try<ClipSyncAI.LlamaBackendFree>("llama_backend_free");
        Try<ClipSyncAI.LlamaModelDefaultParams>("llama_model_default_params");
        Try<ClipSyncAI.LlamaModelLoadFromFile>("llama_model_load_from_file");
        Try<ClipSyncAI.LlamaModelFree>("llama_model_free");
        Try<ClipSyncAI.LlamaModelGetVocab>("llama_model_get_vocab");
        Try<ClipSyncAI.LlamaModelChatTemplate>("llama_model_chat_template");
        Try<ClipSyncAI.LlamaContextDefaultParams>("llama_context_default_params");
        Try<ClipSyncAI.LlamaInitFromModel>("llama_init_from_model");
        Try<ClipSyncAI.LlamaFree>("llama_free");
        Try<ClipSyncAI.LlamaTokenize>("llama_tokenize");
        Try<ClipSyncAI.LlamaTokenToPiece>("llama_token_to_piece");
        Try<ClipSyncAI.LlamaBatchGetOne>("llama_batch_get_one");
        Try<ClipSyncAI.LlamaBatchFree>("llama_batch_free");
        Try<ClipSyncAI.LlamaDecode>("llama_decode");
        Try<ClipSyncAI.LlamaSamplerChainDefaultParams>("llama_sampler_chain_default_params");
        Try<ClipSyncAI.LlamaSamplerChainInit>("llama_sampler_chain_init");
        Try<ClipSyncAI.LlamaSamplerChainAdd>("llama_sampler_chain_add");
        Try<ClipSyncAI.LlamaSamplerInitGreedy>("llama_sampler_init_greedy");
        Try<ClipSyncAI.LlamaSamplerInitDist>("llama_sampler_init_dist");
        Try<ClipSyncAI.LlamaSamplerInitTopP>("llama_sampler_init_top_p");
        Try<ClipSyncAI.LlamaSamplerInitTemp>("llama_sampler_init_temp");
        Try<ClipSyncAI.LlamaSamplerFree>("llama_sampler_free");
        Try<ClipSyncAI.LlamaSamplerSample>("llama_sampler_sample");
        Try<ClipSyncAI.LlamaVocabIsEog>("llama_vocab_is_eog");
        Try<ClipSyncAI.LlamaChatApplyTemplate>("llama_chat_apply_template");
    }
}
