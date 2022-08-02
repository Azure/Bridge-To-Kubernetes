// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export interface IReleasable {
    /** Release the underlying aquired element (meaning that it can free up everything).
     */
    release(): void;
}

export type EventHandler<TEvent> = (event: TEvent) => void;

export interface IReadOnlyEventSource<TEvent> {
    /** Subscribe to this event.
     * @param handler that will be used.
     * @return IReleasable to be able to unsubscribe to the event.
     */
    subscribe(handler: EventHandler<TEvent>): IReleasable;
}

export class EventSource<TEvent> implements IReadOnlyEventSource<TEvent> {
    private _subscribers: EventHandler<TEvent>[];

    constructor() {
        this._subscribers = [];
    }

    /** Subscribe to this event.
     * @param handler that will be used.
     * @return IReleasable to be able to unsubscribe to the event.
     */
    public subscribe(handler: EventHandler<TEvent>): IReleasable {
        this._subscribers.push(handler);
        return {
            release: (): void => {
                // Index of the element could have changed a lot so we need to search for it and remove if we find it.
                const index: number = this._subscribers.indexOf(handler);
                if (index > -1) {
                    this._subscribers.splice(index, 1);
                }
            }
        };
    }

    /** Notify all event subscribers.
     * @param event The event object that will be sent.
     */
    public trigger(event: TEvent): void {
        // Notify our subscribers, looping in reverse order to avoid issues when a subscriber removes itself from the list.
        for (let i = this._subscribers.length - 1; i >= 0; --i) {
            this._subscribers[i](event);
        }
    }
}