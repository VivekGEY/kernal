// Copyright (c) Microsoft. All rights reserved.

import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { AlertType } from '../../../libs/models/AlertType';
import { Alert, Alerts, AppState, LoggedInUserInfo } from './AppState';

const initialState: AppState = {
    alerts: {
        '0': {
            message:
                'SK Copilot is designed for internal use only. By using this chat bot, you agree to not to share confidential or customer information or store sensitive information in chat history. Further, you agree that SK Copilot can collect and retain your chat history for service improvement.',
            type: AlertType.Info,
        },
    },
    loggedInUserInfo: undefined,
};

export const appSlice = createSlice({
    name: 'app',
    initialState,
    reducers: {
        setAlerts: (state: AppState, action: PayloadAction<Alerts>) => {
            state.alerts = action.payload;
        },
        addAlert: (state: AppState, action: PayloadAction<Alert>) => {
            const keys = Object.keys(state.alerts ?? []);
            let newIndex = keys.length.toString();
            if (keys.length >= 3) {
                newIndex = keys[0];
                delete state.alerts![newIndex];
            }
            state.alerts = { ...state.alerts, [newIndex]: action.payload };
        },
        removeAlert: (state: AppState, action: PayloadAction<string>) => {
            if (state.alerts) delete state.alerts[action.payload];
        },
        setLoggedInUserInfo: (state: AppState, action: PayloadAction<LoggedInUserInfo>) => {
            state.loggedInUserInfo = action.payload;
        },
    },
});

export const {
    addAlert,
    removeAlert,
    setAlerts,
    setLoggedInUserInfo
} = appSlice.actions;

export default appSlice.reducer;
