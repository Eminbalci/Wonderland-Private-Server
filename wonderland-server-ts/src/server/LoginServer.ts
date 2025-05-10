import { createServer, Socket } from 'net';
import { config } from '../config/config';
import { PlayerRepository } from '../database/repositories/PlayerRepository';
import { PacketType } from '../network/packets/PacketTypes';
import { PacketParser } from '../network/packets/PacketParser';
import { PlayerSession } from '../network/PlayerSession';
import * as crypto from 'crypto';
import { EventEmitter } from 'events';

interface LoginClient {
    ip: string;
    sockets: Map<string, PlayerSession>;
    addSocket(socket: Socket): PlayerSession | null;
}

export class LoginServer extends EventEmitter {
    private server: any;
    private playerRepository: PlayerRepository;
    private clientList: Map<string, LoginClient>;
    private isRunning: boolean = false;
    private readonly MIN_VERSION = 1096;

    constructor() {
        super();
        this.playerRepository = new PlayerRepository();
        this.clientList = new Map();
        this.server = createServer(this.handleConnection.bind(this));
    }

    private handleConnection(socket: Socket): void {
        const clientIp = socket.remoteAddress || 'unknown';
        console.log('[LoginServer] New connection from:', clientIp, socket.remotePort);

        let loginClient = this.clientList.get(clientIp);
        if (!loginClient) {
            const sockets = new Map<string, PlayerSession>();
            loginClient = {
                ip: clientIp,
                sockets,
                addSocket: (sock: Socket) => {
                    const session = new PlayerSession(sock);
                    sockets.set(session.id, session);
                    return session;
                }
            };
            this.clientList.set(clientIp, loginClient);
            console.log('[LoginServer] Created new client for IP:', clientIp);
        }

        const session = loginClient.addSocket(socket);
        if (!session) {
            console.log('[LoginServer] Failed to create session for IP:', clientIp);
            socket.destroy();
            return;
        }

        console.log('[LoginServer] Created new session:', session.id);

        socket.on('data', async (data: Buffer) => {
            console.log('[LoginServer] Received data from', clientIp, 'Length:', data.length);
            console.log('[LoginServer] First byte:', data.readUInt8(0).toString(16));
            
            if (data.length < 1) {
                console.log('[LoginServer] Data too short');
                return;
            }
            
            const packetType = data.readUInt8(0);
            const packetData = data.slice(1);

            console.log('[LoginServer] Packet type:', packetType.toString(16));

            if (packetType === PacketType.LOGIN_REQUEST) {
                console.log('[LoginServer] Processing login request');
                await this.handleLogin(socket, packetData, session);
            } else {
                console.log('[LoginServer] Unknown packet type:', packetType.toString(16));
            }
        });

        socket.on('close', () => {
            console.log('[LoginServer] Client disconnected:', clientIp, socket.remotePort);
            session.disconnect();
            loginClient.sockets.delete(session.id);
            if (loginClient.sockets.size === 0) {
                this.clientList.delete(clientIp);
                console.log('[LoginServer] Removed client:', clientIp);
            }
        });

        socket.on('error', (err) => {
            console.error('[LoginServer] Socket error:', err);
            session.disconnect();
            loginClient.sockets.delete(session.id);
            if (loginClient.sockets.size === 0) {
                this.clientList.delete(clientIp);
                console.log('[LoginServer] Removed client due to error:', clientIp);
            }
        });
    }

    private async handleLogin(socket: Socket, data: Buffer, session: PlayerSession): Promise<void> {
        console.log('[LoginServer] Starting login process');
        const { username, password, version, loginCode } = PacketParser.parseLoginRequest(data);
        console.log('[LoginServer] Login attempt for username:', username);
        
        try {
            // Validate input
            if (!username || !password || username.length < 4 || username.length > 14 || 
                password.length < 4 || password.length > 14) {
                console.log('[LoginServer] Invalid credentials');
                this.sendLoginResponse(socket, false, 'Invalid username or password');
                return;
            }

            // Validate version
            if (version < this.MIN_VERSION) {
                console.log('[LoginServer] Invalid version:', version);
                this.sendLoginResponse(socket, false, 'Client version too old');
                return;
            }

            // Validate login code
            if (loginCode.length < 2 || loginCode.length > 15) {
                console.log('[LoginServer] Invalid login code length:', loginCode.length);
                this.sendLoginResponse(socket, false, 'Invalid login code');
                return;
            }

            // Find player
            let player = await this.playerRepository.findByUsername(username);
            console.log('[LoginServer] Player found:', !!player);

            if (!player) {
                // Create new account
                console.log('[LoginServer] Creating new account');
                const hashedPassword = this.hashPassword(password);
                player = await this.playerRepository.create(username, hashedPassword);
                this.sendLoginResponse(socket, true, 'Account created successfully');
            } else {
                // Verify password
                console.log('[LoginServer] Verifying password');
                const hashedPassword = this.hashPassword(password);
                if (player.password !== hashedPassword) {
                    console.log('[LoginServer] Invalid password');
                    this.sendLoginResponse(socket, false, 'Invalid password');
                    return;
                }
                this.sendLoginResponse(socket, true, 'Login successful');
            }

            // Update player status
            player.isOnline = true;
            await this.playerRepository.update(player);
            session.setPlayer(player);

            // Emit new player event
            this.emit('newPlayer', player);
            console.log('[LoginServer] Login successful for:', username);

        } catch (error) {
            console.error('[LoginServer] Login error:', error);
            this.sendLoginResponse(socket, false, 'Server error occurred');
        }
    }

    private sendLoginResponse(socket: Socket, success: boolean, message: string): void {
        console.log('[LoginServer] Sending login response:', success, message);
        const response = Buffer.alloc(2 + message.length);
        response.writeUInt8(PacketType.LOGIN_RESPONSE, 0);
        response.writeUInt8(success ? 1 : 0, 1);
        response.write(message, 2);
        socket.write(response);
    }

    private hashPassword(password: string): string {
        return crypto.createHash('sha256').update(password).digest('hex');
    }

    public isOnline(username: string): boolean {
        let count = 0;
        for (const client of this.clientList.values()) {
            for (const session of client.sockets.values()) {
                if (session.player?.username === username) {
                    count++;
                }
            }
        }
        return count > 1;
    }

    public isOnlineByUserId(userId: number): boolean {
        let count = 0;
        for (const client of this.clientList.values()) {
            for (const session of client.sockets.values()) {
                if (session.player?.id === userId) {
                    count++;
                }
            }
        }
        return count > 1;
    }

    public disconnect(userId: number): void {
        for (const client of this.clientList.values()) {
            for (const session of client.sockets.values()) {
                if (session.player?.id === userId) {
                    session.disconnect();
                }
            }
        }
    }

    public start(): void {
        if (this.isRunning) return;

        const PORT = 6414; // Match C# port
        const HOST = '0.0.0.0'; // Match C# binding

        this.server.listen(PORT, HOST, () => {
            console.log(`[LoginServer] Server running on ${HOST}:${PORT}`);
            this.isRunning = true;
        });
    }

    public stop(): void {
        if (!this.isRunning) return;

        this.server.close(() => {
            console.log('[LoginServer] Server stopped');
            this.isRunning = false;
        });
    }
} 