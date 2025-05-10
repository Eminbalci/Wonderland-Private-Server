import { createConnection, Socket } from 'net';
import { config } from '../config/config';

class LoginTest {
    private socket: Socket;

    constructor() {
        this.socket = createConnection({
            host: config.server.host,
            port: Number(config.server.port)
        });

        this.setupSocketListeners();
    }

    private setupSocketListeners(): void {
        this.socket.on('connect', () => {
            console.log('Connected to login server');
            this.testLogin();
        });

        this.socket.on('data', (data: Buffer) => {
            const packetType = data.readUInt8(0);
            const success = data.readUInt8(1);
            const message = data.toString('utf8', 2);

            console.log('Received response:');
            console.log('Packet Type:', packetType);
            console.log('Success:', success === 1);
            console.log('Message:', message);

            if (success === 1) {
                console.log('Login successful!');
            } else {
                console.log('Login failed!');
            }

            this.socket.end();
        });

        this.socket.on('error', (err) => {
            console.error('Socket error:', err);
        });

        this.socket.on('close', () => {
            console.log('Connection closed');
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
        packet.writeUInt8(0x01, offset++);

        // Write username length and data
        packet.writeUInt8(usernameBuffer.length, offset++);
        usernameBuffer.copy(packet, offset);
        offset += usernameBuffer.length;

        // Write password length and data
        packet.writeUInt8(passwordBuffer.length, offset++);
        passwordBuffer.copy(packet, offset);

        // Send login packet
        this.socket.write(packet);
    }
}

// Run the test
new LoginTest(); 