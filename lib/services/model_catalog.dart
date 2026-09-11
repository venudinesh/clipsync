import 'device_info_service.dart';

/// Curated catalog of Ollama-compatible models with device-aware recommendations.
class ModelCatalog {
  static final ModelCatalog _instance = ModelCatalog._();
  factory ModelCatalog() => _instance;
  ModelCatalog._();

  // ── Full Model Catalog ─────────────────────────────────────────────────
  // Each entry: name, family, params (billions), category, description,
  // minRamMB, recommendedRamMB, downloadSizeMB

  static const List<CatalogModel> allModels = [
    // ── Ultra-Light (0.5B - 1B) ──
    CatalogModel(
      name: 'qwen2.5:0.5b',
      family: 'Qwen 2.5',
      paramsBillion: 0.5,
      category: ModelCategory.general,
      description: 'Ultra-light general model. Fast, runs on any device.',
      minRamMB: 1024,
      recommendedRamMB: 2048,
      downloadSizeMB: 400,
      tags: ['fast', 'lightweight', 'multilingual'],
    ),
    CatalogModel(
      name: 'tinyllama:1.1b',
      family: 'TinyLlama',
      paramsBillion: 1.1,
      category: ModelCategory.general,
      description: 'Compact general-purpose model from the Llama family.',
      minRamMB: 1500,
      recommendedRamMB: 2500,
      downloadSizeMB: 650,
      tags: ['fast', 'general', 'llama'],
    ),
    CatalogModel(
      name: 'gemma2:2b',
      family: 'Gemma 2',
      paramsBillion: 2.0,
      category: ModelCategory.general,
      description: 'Google\'s efficient 2B model. Good quality for size.',
      minRamMB: 2048,
      recommendedRamMB: 3500,
      downloadSizeMB: 1600,
      tags: ['google', 'efficient', 'quality'],
    ),

    // ── Light (2B - 3B) ──
    CatalogModel(
      name: 'llama3.2:1b',
      family: 'Llama 3.2',
      paramsBillion: 1.0,
      category: ModelCategory.general,
      description: 'Meta\'s efficient 1B model. Great for mobile.',
      minRamMB: 1500,
      recommendedRamMB: 3000,
      downloadSizeMB: 700,
      tags: ['meta', 'fast', 'mobile', 'popular'],
    ),
    CatalogModel(
      name: 'llama3.2:3b',
      family: 'Llama 3.2',
      paramsBillion: 3.0,
      category: ModelCategory.general,
      description: 'Meta\'s 3B model. Balanced speed and quality.',
      minRamMB: 3000,
      recommendedRamMB: 4500,
      downloadSizeMB: 2000,
      tags: ['meta', 'balanced', 'popular'],
    ),
    CatalogModel(
      name: 'phi3:mini',
      family: 'Phi-3',
      paramsBillion: 3.8,
      category: ModelCategory.general,
      description: 'Microsoft\'s compact but capable model.',
      minRamMB: 3500,
      recommendedRamMB: 5000,
      downloadSizeMB: 2200,
      tags: ['microsoft', 'capable', 'reasoning'],
    ),
    CatalogModel(
      name: 'gemma2:4b',
      family: 'Gemma 2',
      paramsBillion: 4.0,
      category: ModelCategory.general,
      description: 'Google\'s 4B model. Strong for its size.',
      minRamMB: 4000,
      recommendedRamMB: 6000,
      downloadSizeMB: 2500,
      tags: ['google', 'quality', 'reasoning'],
    ),

    // ── Medium (7B - 8B) ──
    CatalogModel(
      name: 'llama3.1:8b',
      family: 'Llama 3.1',
      paramsBillion: 8.0,
      category: ModelCategory.general,
      description: 'Meta\'s flagship 8B model. Excellent quality.',
      minRamMB: 6000,
      recommendedRamMB: 8000,
      downloadSizeMB: 4700,
      tags: ['meta', 'high-quality', 'popular', 'recommended'],
    ),
    CatalogModel(
      name: 'mistral:7b',
      family: 'Mistral',
      paramsBillion: 7.0,
      category: ModelCategory.general,
      description: 'Mistral AI\'s 7B model. Fast inference.',
      minRamMB: 5500,
      recommendedRamMB: 7500,
      downloadSizeMB: 4100,
      tags: ['mistral', 'fast', 'quality'],
    ),
    CatalogModel(
      name: 'codellama:7b',
      family: 'Code Llama',
      paramsBillion: 7.0,
      category: ModelCategory.coding,
      description: 'Code-specialized model. Great for code completion.',
      minRamMB: 5500,
      recommendedRamMB: 7500,
      downloadSizeMB: 3800,
      tags: ['coding', 'code', 'programming'],
    ),

    // ── Code-Specific ──
    CatalogModel(
      name: 'codellama:3b',
      family: 'Code Llama',
      paramsBillion: 3.0,
      category: ModelCategory.coding,
      description: 'Lightweight code model. Good for quick suggestions.',
      minRamMB: 3000,
      recommendedRamMB: 4500,
      downloadSizeMB: 1900,
      tags: ['coding', 'code', 'lightweight'],
    ),
    CatalogModel(
      name: 'qwen2.5-coder:3b',
      family: 'Qwen 2.5 Coder',
      paramsBillion: 3.0,
      category: ModelCategory.coding,
      description: 'Excellent code model. Strong multilingual coding.',
      minRamMB: 3000,
      recommendedRamMB: 4500,
      downloadSizeMB: 1900,
      tags: ['coding', 'code', 'multilingual'],
    ),
    CatalogModel(
      name: 'deepseek-coder:6.7b',
      family: 'DeepSeek Coder',
      paramsBillion: 6.7,
      category: ModelCategory.coding,
      description: 'Specialized code generation model.',
      minRamMB: 5500,
      recommendedRamMB: 7500,
      downloadSizeMB: 3800,
      tags: ['coding', 'code', 'high-quality'],
    ),

    // ── Conversation / Chat ──
    CatalogModel(
      name: 'gemma2:9b',
      family: 'Gemma 2',
      paramsBillion: 9.0,
      category: ModelCategory.conversation,
      description: 'Google\'s 9B model. Excellent for conversations.',
      minRamMB: 7000,
      recommendedRamMB: 10000,
      downloadSizeMB: 5500,
      tags: ['google', 'conversation', 'high-quality'],
    ),
    CatalogModel(
      name: 'llama3.1:70b',
      family: 'Llama 3.1',
      paramsBillion: 70.0,
      category: ModelCategory.conversation,
      description: 'Meta\'s massive 70B model. Near GPT-4 quality.',
      minRamMB: 48000,
      recommendedRamMB: 64000,
      downloadSizeMB: 40000,
      tags: ['meta', 'huge', 'best-quality', 'requires-gpu'],
    ),

    // ── Embedding ──
    CatalogModel(
      name: 'nomic-embed-text',
      family: 'Nomic Embed',
      paramsBillion: 0.1,
      category: ModelCategory.embedding,
      description: 'Text embedding model for RAG and search.',
      minRamMB: 512,
      recommendedRamMB: 1024,
      downloadSizeMB: 275,
      tags: ['embedding', 'search', 'rag', 'lightweight'],
    ),
  ];

  // ── Recommendation Engine ──────────────────────────────────────────────

  /// Get all models that can run on this device
  List<CatalogModel> getRunnableModels(DeviceProfile device) {
    return allModels
        .where((m) => m.minRamMB <= device.totalRamMB)
        .toList()
      ..sort((a, b) => a.paramsBillion.compareTo(b.paramsBillion));
  }

  /// Get recommended models (best balance for this device)
  List<CatalogModel> getRecommendedModels(DeviceProfile device) {
    return allModels
        .where((m) =>
            m.minRamMB <= device.totalRamMB &&
            m.downloadSizeMB <= device.availableStorageGB * 1024 &&
            m.paramsBillion <= device.maxRecommendedParams)
        .toList()
      ..sort((a, b) {
        // Prefer models closest to the device's sweet spot
        final aFit = (a.paramsBillion - device.maxRecommendedParams * 0.7).abs();
        final bFit = (b.paramsBillion - device.maxRecommendedParams * 0.7).abs();
        return aFit.compareTo(bFit);
      });
  }

  /// Get models by category
  List<CatalogModel> getByCategory(ModelCategory category) {
    return allModels.where((m) => m.category == category).toList();
  }

  /// Search models by name or tag
  List<CatalogModel> search(String query) {
    final q = query.toLowerCase();
    return allModels.where((m) {
      return m.name.toLowerCase().contains(q) ||
          m.family.toLowerCase().contains(q) ||
          m.description.toLowerCase().contains(q) ||
          m.tags.any((t) => t.contains(q));
    }).toList();
  }

  /// Check if a model is compatible with the device
  ModelCompatibility getCompatibility(CatalogModel model, DeviceProfile device) {
    if (model.minRamMB > device.totalRamMB) {
      return ModelCompatibility.insufficientRam;
    }
    if (model.downloadSizeMB > device.availableStorageGB * 1024) {
      return ModelCompatibility.insufficientStorage;
    }
    if (model.paramsBillion > device.maxRecommendedParams * 1.5) {
      return ModelCompatibility.tooLarge;
    }
    if (model.paramsBillion <= device.maxRecommendedParams) {
      return ModelCompatibility.recommended;
    }
    return ModelCompatibility.compatible;
  }

  /// Estimate inference speed for a model on this device
  int estimateTokensPerSec(CatalogModel model, DeviceProfile device) {
    final baseSpeed = device.estimatedTokensPerSec;
    // Larger models are slower
    final sizeFactor = 1.0 / (model.paramsBillion / 2.0).clamp(0.5, 8.0);
    // Quantization helps
    final quantBoost = device.totalRamMB >= 8192 ? 1.2 : 1.0;
    return (baseSpeed * sizeFactor * quantBoost).toInt().clamp(1, 100);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS
// ─────────────────────────────────────────────────────────────────────────────

class CatalogModel {
  final String name;
  final String family;
  final double paramsBillion;
  final ModelCategory category;
  final String description;
  final int minRamMB;
  final int recommendedRamMB;
  final int downloadSizeMB;
  final List<String> tags;

  const CatalogModel({
    required this.name,
    required this.family,
    required this.paramsBillion,
    required this.category,
    required this.description,
    required this.minRamMB,
    required this.recommendedRamMB,
    required this.downloadSizeMB,
    this.tags = const [],
  });

  String get displayName {
    if (name.endsWith(':latest')) return name.substring(0, name.length - 7);
    return name;
  }

  String get formattedDownloadSize {
    if (downloadSizeMB >= 1024) {
      return '${(downloadSizeMB / 1024).toStringAsFixed(1)} GB';
    }
    return '$downloadSizeMB MB';
  }

  String get formattedParams {
    if (paramsBillion < 1) return '${(paramsBillion * 1000).toInt()}M';
    return '${paramsBillion.toStringAsFixed(1)}B';
  }
}

enum ModelCategory {
  general,
  coding,
  conversation,
  embedding,
}

enum ModelCompatibility {
  recommended,      // Ideal for this device
  compatible,       // Can run but not ideal
  tooLarge,         // Will be slow / may run out of RAM
  insufficientRam,  // Not enough RAM
  insufficientStorage, // Not enough storage
}
