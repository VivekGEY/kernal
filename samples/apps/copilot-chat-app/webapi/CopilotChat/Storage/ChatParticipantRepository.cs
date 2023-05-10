﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SemanticKernel.Service.CopilotChat.Models;

namespace SemanticKernel.Service.CopilotChat.Storage;

/// <summary>
/// A repository for chat sessions.
/// </summary>
public class ChatParticipantRepository : Repository<ChatParticipant>
{
    /// <summary>
    /// Initializes a new instance of the ChatParticipantRepository class.
    /// </summary>
    /// <param name="storageContext">The storage context.</param>
    public ChatParticipantRepository(IStorageContext<ChatParticipant> storageContext)
        : base(storageContext)
    {
    }

    /// <summary>
    /// Finds chat participants by user id.
    /// A user can be part of multiple chats, thus a user can have multiple chat participants.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>A list of chat participants of the same user id in different chat sessions.</returns>
    public Task<IEnumerable<ChatParticipant>> FindByUserIdAsync(string userId)
    {
        return base.StorageContext.QueryEntitiesAsync(e => e.UserId == userId);
    }

    public Task<bool> IsUserInChatAsync(string userId, string chatId)
    {
        return base.StorageContext.QueryEntitiesAsync(e => e.UserId == userId && e.ChatId == chatId).
            ContinueWith(t => t.Result.Any(), TaskScheduler.Default);
    }
}
