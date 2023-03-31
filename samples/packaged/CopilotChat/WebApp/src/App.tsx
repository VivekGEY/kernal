// Copyright (c) Microsoft. All rights reserved.

import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import { Avatar, makeStyles, Subtitle1 } from '@fluentui/react-components';
import * as React from 'react';
import { FC, useEffect } from 'react';
import { Login } from './components/Login';
import { ChatView } from './components/views/ChatView';
import FunctionProbe from './components/views/FunctionProbe';
import { msalInstance } from './main';
import { useAppDispatch, useAppSelector } from './redux/app/hooks';
import { RootState } from './redux/app/store';
import { setSelectedConversation } from './redux/features/conversations/conversationsSlice';

const useClasses = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'stretch',
        justifyContent: 'space-between',
    },
    header: {
        backgroundColor: '#9c2153',
        width: '100%',
        height: '48px',
        color: '#FFF',
        display: 'flex',
        '& h1': {
            paddingLeft: '20px',
            display: 'flex',
        },
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    persona: {
        marginRight: '20px'
    }
});

enum AppState {
    ProbeForFunction,
    Chat
}

const App: FC = () => {
    const [appState, setAppState] = React.useState(AppState.ProbeForFunction);
    const classes = useClasses();
    const { conversations } = useAppSelector((state: RootState) => state.conversations);
    const dispatch = useAppDispatch();
    const account = msalInstance.getActiveAccount();

    useEffect(() => {
        // TODO: Load Conversations from BE
        const keys = Object.keys(conversations);
        dispatch(setSelectedConversation(keys[0]));
    }, []);

    return (
        <div>
            <UnauthenticatedTemplate>
                <Login />
            </UnauthenticatedTemplate>
            <AuthenticatedTemplate>
                {appState === AppState.ProbeForFunction &&
                    <FunctionProbe
                        uri={import.meta.env.VITE_REACT_APP_FUNCTION_URI as string}
                        onFunctionFound={() => setAppState(AppState.Chat)}
                    />
                }
                {appState === AppState.Chat &&
                    <div style={{ display: 'flex', width: '100%', flexDirection: 'column', height: '100vh' }}>
                        <div className={classes.header} >
                            <Subtitle1 as="h1">Copilot Chat</Subtitle1>
                            <Avatar
                                className={classes.persona}
                                key={account?.name}
                                name={account?.name}
                                size={28}
                                badge={{ status: 'available' }}
                            />
                        </div>
                        <ChatView />
                    </div>
                }
            </AuthenticatedTemplate>
        </div>
    );
};

export default App;
