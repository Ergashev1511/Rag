namespace Rag.Application.Ingestion;

/// <summary>Qayta urinish foyda bermaydigan xato (masalan, skaner PDF).</summary>
public sealed class IngestionException(string message) : Exception(message);
