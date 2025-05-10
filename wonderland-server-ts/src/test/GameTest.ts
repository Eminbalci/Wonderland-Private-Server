import { createConnection, Socket } from 'net';
import { config } from '../config/config';
import { PacketType } from '../network/packets/PacketTypes';

class GameTest {
    private loginSocket: Socket;
    private gameSocket: Socket | null = null;
    private sessionId: string | null = null;

    constructor() {
        this.loginSocket = createConnection({
            host: config.server.host,
            port: Number(config.server.port)
        });

        this.setupLoginSocketListeners();
    }

    private setupLoginSocketListeners(): void {
        this.loginSocket.on('connect', () => {
            console.log('Connected to login server');
            this.testLogin();
        });

        this.loginSocket.on('data', (data: Buffer) => {
            const packetType = data.readUInt8(0);
            const success = data.readUInt8(1);
            const message = data.toString('utf8', 2);

            console.log('Received login response:');
            console.log('Packet Type:', packetType);
            console.log('Success:', success === 1);
            console.log('Message:', message);

            if (success === 1) {
                console.log('Login successful!');
                this.connectToGameServer();
            } else {
                console.log('Login failed!');
                this.loginSocket.end();
            }
        });

        this.loginSocket.on('error', (err) => {
            console.error('Login socket error:', err);
        });

        this.loginSocket.on('close', () => {
            console.log('Login connection closed');
        });
    }

    private setupGameSocketListeners(): void {
        if (!this.gameSocket) return;

        this.gameSocket.on('connect', () => {
            console.log('Connected to game server');
            this.requestCharacterList();
        });

        this.gameSocket.on('data', (data: Buffer) => {
            const packetType = data.readUInt8(0);
            
            switch (packetType) {
                case PacketType.CHARACTER_LIST_RESPONSE:
                    this.handleCharacterList(data);
                    break;
                case PacketType.CHARACTER_CREATE_RESPONSE:
                    this.handleCharacterCreate(data);
                    break;
                case PacketType.CHARACTER_DELETE_RESPONSE:
                    this.handleCharacterDelete(data);
                    break;
                case PacketType.MOVE_RESPONSE:
                    this.handleMovement(data);
                    break;
                case PacketType.CHAT_RESPONSE:
                    this.handleChat(data);
                    break;
                case PacketType.PONG:
                    this.handlePong();
                    break;
                case PacketType.ERROR:
                    this.handleError(data);
                    break;
                default:
                    console.warn('Unknown packet type:', packetType);
            }
        });

        this.gameSocket.on('error', (err) => {
            console.error('Game socket error:', err);
        });

        this.gameSocket.on('close', () => {
            console.log('Game connection closed');
        });
    }

    private testLogin(): void {
        const username = 'testuser';
        const password = 'testpass';

        // Create login packet
        const usernameBuffer = Buffer.from(username, 'utf8');
        const passwordBuffer = Buffer.from(password, 'utf8');
        
        const packet = Buffer.alloc(1 + 1 + usernameBuffer.length + 1 + passwordBuffer.length);
        let offset = 0;

        // Write packet type (LOGIN_REQUEST = 0x01)
        packet.writeUInt8(PacketType.LOGIN_REQUEST, offset++);

        // Write username length and data
        packet.writeUInt8(usernameBuffer.length, offset++);
        usernameBuffer.copy(packet, offset);
        offset += usernameBuffer.length;

        // Write password length and data
        packet.writeUInt8(passwordBuffer.length, offset++);
        passwordBuffer.copy(packet, offset);

        // Send login packet
        this.loginSocket.write(packet);
    }

    private connectToGameServer(): void {
        this.gameSocket = createConnection({
            host: config.server.host,
            port: Number(config.server.port) + 1
        });

        this.setupGameSocketListeners();
    }

    private requestCharacterList(): void {
        if (!this.gameSocket) return;

        const packet = Buffer.alloc(1);
        packet.writeUInt8(PacketType.CHARACTER_LIST_REQUEST, 0);
        this.gameSocket.write(packet);
    }

    private handleCharacterList(data: Buffer): void {
        const characterCount = data.readUInt8(1);
        let offset = 2;

        console.log('\nCharacter List:');
        console.log('Total characters:', characterCount);

        for (let i = 0; i < characterCount; i++) {
            const id = data.readUInt32LE(offset);
            offset += 4;

            const name = data.toString('utf8', offset, offset + 16).replace(/\0/g, '');
            offset += 16;

            const level = data.readUInt8(offset++);
            const characterClass = data.readUInt8(offset++);
            const x = data.readUInt8(offset++);
            const y = data.readUInt8(offset++);
            const z = data.readUInt8(offset++);

            console.log(`\nCharacter ${i + 1}:`);
            console.log('ID:', id);
            console.log('Name:', name);
            console.log('Level:', level);
            console.log('Class:', characterClass);
            console.log('Position:', { x, y, z });
        }

        // Close connections after receiving character list
        this.gameSocket?.end();
        this.loginSocket.end();
    }

    private handleCharacterCreate(data: Buffer): void {
        const success = data.readUInt8(1);
        console.log('Character creation:', success === 1 ? 'Success' : 'Failed');
    }

    private handleCharacterDelete(data: Buffer): void {
        const success = data.readUInt8(1);
        console.log('Character deletion:', success === 1 ? 'Success' : 'Failed');
    }

    private handleMovement(data: Buffer): void {
        const success = data.readUInt8(1);
        console.log('Movement:', success === 1 ? 'Success' : 'Failed');
    }

    private handleChat(data: Buffer): void {
        const success = data.readUInt8(1);
        console.log('Chat:', success === 1 ? 'Success' : 'Failed');
    }

    private handlePong(): void {
        console.log('Received pong');
    }

    private handleError(data: Buffer): void {
        const message = data.toString('utf8', 1);
        console.error('Server error:', message);
    }
}

// Run the test
new GameTest(); 