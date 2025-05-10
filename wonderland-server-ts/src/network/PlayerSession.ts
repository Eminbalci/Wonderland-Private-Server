import { Socket } from 'net';
import { v4 as uuidv4 } from 'uuid';
import { Player } from '../database/entities/Player';

export class PlayerSession {
    public readonly id: string;
    private socket: Socket;
    private _player: Player | null;
    private lastPing: number = Date.now();

    constructor(socket: Socket) {
        this.id = uuidv4();
        this.socket = socket;
        this._player = null;
    }

    public get player(): Player | null {
        return this._player;
    }

    public setPlayer(player: Player): void {
        this._player = player;
    }

    public send(data: Buffer): void {
        if (this.socket.writable) {
            this.socket.write(data);
        }
    }

    public disconnect(): void {
        if (this._player) {
            this._player.isOnline = false;
            // TODO: Save player state
        }
        this.socket.destroy();
    }

    updateLastPing(): void {
        this.lastPing = Date.now();
    }

    getLastPing(): number {
        return this.lastPing;
    }

    static getSession(sessionId: string): PlayerSession | undefined {
        // Implementation of static getSession method
        return undefined; // Placeholder return, actual implementation needed
    }

    static getAllSessions(): PlayerSession[] {
        // Implementation of static getAllSessions method
        return []; // Placeholder return, actual implementation needed
    }

    static getOnlinePlayers(): Player[] {
        // Implementation of static getOnlinePlayers method
        return []; // Placeholder return, actual implementation needed
    }
} 