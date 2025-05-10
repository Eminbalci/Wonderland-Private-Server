import { createServer, Socket } from 'net';
import { config } from '../config/config';
import { LoginServer } from './LoginServer';
import { PlayerSession } from '../network/PlayerSession';
import { PacketType } from '../network/packets/PacketTypes';
import { PacketParser } from '../network/packets/PacketParser';

export class GameServer {
    private server: any;
    private loginServer: LoginServer;
    private sessions: Map<string, PlayerSession>;

    constructor() {
        this.server = createServer(this.handleConnection.bind(this));
        this.loginServer = new LoginServer();
        this.sessions = new Map();
    }

    private handleConnection(socket: Socket): void {
        console.log('Game client connected:', socket.remoteAddress, socket.remotePort);
        const session = new PlayerSession(socket);
        this.sessions.set(session.id, session);

        socket.on('data', async (data: Buffer) => {
            if (data.length < 1) return;
            
            const packetType = data.readUInt8(0);
            const packetData = data.slice(1);

            try {
                switch (packetType) {
                    case PacketType.CHARACTER_LIST_REQUEST:
                        await this.handleCharacterList(session);
                        break;
                    case PacketType.CHARACTER_CREATE_REQUEST:
                        await this.handleCharacterCreate(session, packetData);
                        break;
                    case PacketType.CHARACTER_DELETE_REQUEST:
                        await this.handleCharacterDelete(session, packetData);
                        break;
                    case PacketType.MOVE_REQUEST:
                        await this.handleMovement(session, packetData);
                        break;
                    case PacketType.CHAT_MESSAGE:
                        await this.handleChat(session, packetData);
                        break;
                    case PacketType.PING:
                        this.handlePing(session);
                        break;
                    default:
                        console.warn('Unknown packet type:', packetType);
                }
            } catch (error) {
                console.error('Error handling packet:', error);
                session.send(PacketParser.createErrorResponse('Server error occurred'));
            }
        });

        socket.on('close', () => {
            console.log('Game client disconnected:', socket.remoteAddress, socket.remotePort);
            this.sessions.delete(session.id);
            session.disconnect();
        });

        socket.on('error', (err) => {
            console.error('Game socket error:', err);
            this.sessions.delete(session.id);
            session.disconnect();
        });
    }

    private async handleCharacterList(session: PlayerSession): Promise<void> {
        if (!session.player) {
            session.send(PacketParser.createErrorResponse('Not authenticated'));
            return;
        }

        try {
            const characters = await session.player.getCharacters();
            
            // Create character list response packet
            const characterCount = characters.length;
            const packetSize = 2 + (characterCount * 25); // 2 bytes for header, 25 bytes per character
            const response = Buffer.alloc(packetSize);
            let offset = 0;

            // Write packet type and character count
            response.writeUInt8(PacketType.CHARACTER_LIST_RESPONSE, offset++);
            response.writeUInt8(characterCount, offset++);

            // Write each character's data
            for (const character of characters) {
                // Write character ID (4 bytes)
                response.writeUInt32LE(character.id, offset);
                offset += 4;

                // Write character name (16 bytes, padded with nulls)
                const nameBuffer = Buffer.from(character.name, 'utf8');
                nameBuffer.copy(response, offset);
                offset += 16;

                // Write character level (1 byte)
                response.writeUInt8(character.level, offset++);

                // Write character class (1 byte)
                response.writeUInt8(0, offset++); // TODO: Add class to Character entity

                // Write character position (3 bytes)
                response.writeUInt8(Math.floor(character.x), offset++);
                response.writeUInt8(Math.floor(character.y), offset++);
                response.writeUInt8(Math.floor(character.z), offset++);
            }

            session.send(response);
        } catch (error) {
            console.error('Error getting character list:', error);
            session.send(PacketParser.createErrorResponse('Failed to get character list'));
        }
    }

    private async handleCharacterCreate(session: PlayerSession, data: Buffer): Promise<void> {
        if (!session.player) {
            session.send(PacketParser.createErrorResponse('Not authenticated'));
            return;
        }

        try {
            const { name, class: characterClass, gender, hairStyle, hairColor } = PacketParser.parseCharacterCreate(data);
            
            // TODO: Create character in database
            // For now, just send success response
            const response = Buffer.alloc(2);
            response.writeUInt8(PacketType.CHARACTER_CREATE_RESPONSE, 0);
            response.writeUInt8(1, 1); // Success
            session.send(response);
        } catch (error) {
            console.error('Error creating character:', error);
            session.send(PacketParser.createErrorResponse('Failed to create character'));
        }
    }

    private async handleCharacterDelete(session: PlayerSession, data: Buffer): Promise<void> {
        if (!session.player) {
            session.send(PacketParser.createErrorResponse('Not authenticated'));
            return;
        }

        try {
            const { characterId, password } = PacketParser.parseCharacterDelete(data);
            
            // TODO: Delete character from database
            // For now, just send success response
            const response = Buffer.alloc(2);
            response.writeUInt8(PacketType.CHARACTER_DELETE_RESPONSE, 0);
            response.writeUInt8(1, 1); // Success
            session.send(response);
        } catch (error) {
            console.error('Error deleting character:', error);
            session.send(PacketParser.createErrorResponse('Failed to delete character'));
        }
    }

    private async handleMovement(session: PlayerSession, data: Buffer): Promise<void> {
        if (!session.player) {
            session.send(PacketParser.createErrorResponse('Not authenticated'));
            return;
        }

        try {
            const { x, y, direction, timestamp } = PacketParser.parseMove(data);
            
            // TODO: Update character position in database
            // For now, just send success response
            const response = Buffer.alloc(2);
            response.writeUInt8(PacketType.MOVE_RESPONSE, 0);
            response.writeUInt8(1, 1); // Success
            session.send(response);
        } catch (error) {
            console.error('Error handling movement:', error);
            session.send(PacketParser.createErrorResponse('Failed to process movement'));
        }
    }

    private async handleChat(session: PlayerSession, data: Buffer): Promise<void> {
        if (!session.player) {
            session.send(PacketParser.createErrorResponse('Not authenticated'));
            return;
        }

        try {
            const { type, target, message } = PacketParser.parseChat(data);
            
            // TODO: Process chat message
            // For now, just send success response
            const response = Buffer.alloc(2);
            response.writeUInt8(PacketType.CHAT_RESPONSE, 0);
            response.writeUInt8(1, 1); // Success
            session.send(response);
        } catch (error) {
            console.error('Error handling chat:', error);
            session.send(PacketParser.createErrorResponse('Failed to process chat message'));
        }
    }

    private handlePing(session: PlayerSession): void {
        const response = Buffer.alloc(1);
        response.writeUInt8(PacketType.PONG, 0);
        session.send(response);
    }

    public start(): void {
        const PORT = Number(config.server.port) + 1; // Game server runs on port + 1
        const HOST = config.server.host;

        // Start login server first
        this.loginServer.start();

        // Then start game server
        this.server.listen(PORT, HOST, () => {
            console.log(`Game server running on ${HOST}:${PORT}`);
        });
    }
} 