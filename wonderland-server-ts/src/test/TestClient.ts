import { Socket } from 'net';
import { PacketType } from '../network/packets/PacketTypes';

export class TestClient {
    private socket: Socket;
    private connected: boolean = false;

    constructor() {
        this.socket = new Socket();
        this.setupSocketListeners();
    }

    private setupSocketListeners(): void {
        this.socket.on('connect', () => {
            console.log('[TestClient] Connected to server');
            this.connected = true;
        });

        this.socket.on('data', (data: Buffer) => {
            console.log('[TestClient] Received data:', data);
            console.log('[TestClient] First byte:', data.readUInt8(0).toString(16));
            
            if (data.length < 2) {
                console.log('[TestClient] Response too short');
                return;
            }

            const packetType = data.readUInt8(0);
            const success = data.readUInt8(1) === 1;
            const message = data.slice(2).toString();

            console.log('[TestClient] Packet type:', packetType.toString(16));
            console.log('[TestClient] Success:', success);
            console.log('[TestClient] Message:', message);
        });

        this.socket.on('close', () => {
            console.log('[TestClient] Connection closed');
            this.connected = false;
        });

        this.socket.on('error', (err) => {
            console.error('[TestClient] Socket error:', err);
            this.connected = false;
        });
    }

    public connect(host: string = 'localhost', port: number = 6414): void {
        console.log('[TestClient] Connecting to', host, port);
        this.socket.connect(port, host);
    }

    public sendLoginRequest(username: string, password: string): void {
        if (!this.connected) {
            console.log('[TestClient] Not connected, cannot send login request');
            return;
        }

        console.log('[TestClient] Sending login request for:', username);
        
        // Create login packet
        const usernameBuffer = Buffer.from(username);
        const passwordBuffer = Buffer.from(password);
        const version = 1096; // Minimum version from C# code
        const loginCode = 'test123'; // Example login code
        const key = 0x55; // Example key
        const loginCodeBuffer = Buffer.from(loginCode);

        const packet = Buffer.alloc(1 + 2 + usernameBuffer.length + 2 + passwordBuffer.length + 2 + 1 + 1 + loginCodeBuffer.length);

        let offset = 0;
        packet.writeUInt8(PacketType.LOGIN_REQUEST, offset++);
        packet.writeUInt16LE(usernameBuffer.length, offset);
        offset += 2;
        usernameBuffer.copy(packet, offset);
        offset += usernameBuffer.length;
        packet.writeUInt16LE(passwordBuffer.length, offset);
        offset += 2;
        passwordBuffer.copy(packet, offset);
        offset += passwordBuffer.length;
        packet.writeUInt16LE(version, offset);
        offset += 2;
        packet.writeUInt8(loginCodeBuffer.length, offset++);
        packet.writeUInt8(key, offset++);
        loginCodeBuffer.copy(packet, offset);

        console.log('[TestClient] Sending packet:', packet);
        this.socket.write(packet);
    }

    public disconnect(): void {
        if (this.connected) {
            this.socket.end();
        }
    }
} 