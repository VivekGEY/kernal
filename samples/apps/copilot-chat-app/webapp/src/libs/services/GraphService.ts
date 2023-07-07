// Copyright (c) Microsoft. All rights reserved.

import { BaseService } from './BaseService';

export interface BatchRequest {
    id: string;
    method: 'GET' | 'POST' | 'PUT' | 'UPDATE' | 'DELETE';
    url: string;
    headers: any;
}

export interface BatchResponseBody {
    responses: BatchResponse[];
}

export interface BatchResponse {
    id: number;
    status: number;
    body?: any;
    headers?: any;
}

export class GraphService extends BaseService {
    constructor() {
        super('https://graph.microsoft.com');
    }

    public makeBatchRequest = async (batchRequests: BatchRequest[], accessToken: string) => {
        const result = await this.getResponseAsync<BatchResponseBody>(
            {
                commandPath: '/v1.0/$batch',
                method: 'POST',
                body: { requests: batchRequests },
            },
            accessToken,
        );

        return result.responses;
    };

    public loadUser = async (userId: string, accessToken: string) => {
        const result = await this.getResponseAsync<object>(
            {
                commandPath: `/users/${userId}`,
                method: 'GET',
            },
            accessToken,
        );

        return result;
    };
}
