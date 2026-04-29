using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface ISpacedRepetitionService
{
    Task<List<SpacedRepetitionItem>> GetDueItemsAsync(Guid userId);
    Task ProcessReviewAsync(Guid itemId, int quality);
    Task<SpacedRepetitionItem> AddItemAsync(Guid userId, Guid knowledgeNodeId);
}
