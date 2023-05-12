// Copyright (c) Microsoft. All rights reserved.

import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { ChatMessageState, IChatMessage } from '../../../libs/models/ChatMessage';
import { ChatState, ConversationTypingState } from './ChatState';
import { Conversations, ConversationsState, ConversationTitleChange, initialState } from './ConversationsState';

export const conversationsSlice = createSlice({
    name: 'conversations',
    initialState,
    reducers: {
        setConversations: (state: ConversationsState, action: PayloadAction<Conversations>) => {
            state.conversations = action.payload;
        },
        editConversationTitle: (state: ConversationsState, action: PayloadAction<ConversationTitleChange>) => {
            const id = action.payload.id;
            const newTitle = action.payload.newTitle;
            state.conversations[id].title = newTitle;
            frontLoadChat(state, id);
        },
        setSelectedConversation: (state: ConversationsState, action: PayloadAction<string>) => {
            state.selectedId = action.payload;
        },
        addConversation: (state: ConversationsState, action: PayloadAction<ChatState>) => {
            const newId = action.payload.id ?? '';
            state.conversations = { [newId]: action.payload, ...state.conversations };
        },
        /*
        * updateConversationFromUser() and updateConversationFromServer() both update the conversations state.
        * However they are for different purposes. The former action is for updating the conversation from the
        * user and will be captured by the SignalR middleware and the payload will be broadcasted to all clients
        * in the same group.
        * The updateConversationFromUser() action is triggered by the SignalR middleware when a response is received.
        */
        updateConversationFromUser: (
            state: ConversationsState,
            action: PayloadAction<{ message: IChatMessage; chatId?: string }>,
        ) => {
            const { message, chatId } = action.payload;
            const id = chatId ?? state.selectedId;
            state.conversations[id].messages.push(message);
            frontLoadChat(state, id);
        },
        updateConversationFromServer: (
            state: ConversationsState,
            action: PayloadAction<{ message: IChatMessage; chatId: string }>,
        ) => {
            const { message, chatId } = action.payload;
            const id = chatId;
            state.conversations[id].messages.push(message);
            frontLoadChat(state, id);
        },
        updateMessageState: (
            state: ConversationsState,
            action: PayloadAction<{ newMessageState: ChatMessageState; messageIndex: number; chatId?: string }>,
        ) => {
            const { newMessageState, messageIndex, chatId } = action.payload;
            const id = chatId ?? state.selectedId;
            state.conversations[id].messages[messageIndex].state = newMessageState;
            frontLoadChat(state, id);
        },
        updateIsTypingFromUser: (state: ConversationsState, action: PayloadAction<ConversationTypingState>) => {
            const id = action.payload.id;
            const isTyping = action.payload.isTyping;
            state.conversations[id].isTyping = isTyping;
            frontLoadChat(state, id);
        },
        updateIsTypingFromServer: (state: ConversationsState, action: PayloadAction<ConversationTypingState>) => {
            const id = action.payload.id;
            const isTyping = action.payload.isTyping;
            state.conversations[id].isTyping = isTyping;
        },
    },
});

export const {
    setConversations,
    editConversationTitle,
    setSelectedConversation,
    addConversation,
    updateConversationFromUser,
    updateConversationFromServer,
    updateMessageState,
    updateIsTypingFromUser,
    updateIsTypingFromServer,
} = conversationsSlice.actions;

export default conversationsSlice.reducer;

const frontLoadChat = (state: ConversationsState, id: string) => {
    const conversation = state.conversations[id];
    delete state.conversations[id];
    state.conversations = { [id]: conversation, ...state.conversations };
};
