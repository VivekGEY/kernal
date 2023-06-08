// Copyright (c) Microsoft. All rights reserved.

import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import React from 'react';
import { IChatMessage } from '../../libs/models/ChatMessage';
import { IAskVariables } from '../../libs/semantic-kernel/model/Ask';
import { ChatHistoryItem } from './ChatHistoryItem';
import { ChatStatus } from './ChatStatus';

const useClasses = makeStyles({
    root: {
        ...shorthands.gap(tokens.spacingVerticalM),
        display: 'flex',
        flexDirection: 'column',
        maxWidth: '900px',
        width: '100%',
        justifySelf: 'center',
    },
    item: {
        display: 'flex',
        flexDirection: 'column',
    },
});

interface ChatHistoryProps {
    messages: IChatMessage[];
    onGetResponse: (
        value: string,
        contextVariables?: IAskVariables[],
        userApprovedPlan?: boolean,
        approvedPlanJson?: string,
        planUserIntent?: string,
    ) => Promise<void>;
}

export const ChatHistory: React.FC<ChatHistoryProps> = ({ messages, onGetResponse }) => {
    const classes = useClasses();

    return (
        <div className={classes.root}>
            {messages
                .slice()
                .sort((a, b) => a.timestamp - b.timestamp)
                .map((message, index) => (
                    <ChatHistoryItem
                        key={message.timestamp}
                        message={message}
                        getResponse={onGetResponse}
                        messageIndex={index}
                    />
                ))}
            <ChatStatus />
        </div>
    );
};
