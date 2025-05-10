import { 
    LoginRequestPacket, 
    CharacterCreatePacket, 
    CharacterDeletePacket,
    MovePacket,
    ChatPacket,
    AttackPacket,
    ItemPacket,
    DropItemPacket,
    EquipItemPacket,
    InventoryUpdatePacket,
    EmotePacket,
    PartyPacket,
    GuildPacket
} from './PacketStructure';
import { PacketType } from './PacketTypes';

export class PacketParser {
    public static parseLoginRequest(data: Buffer): { 
        username: string; 
        password: string;
        version: number;
        loginCode: string;
    } {
        console.log('[PacketParser] Parsing login request, data length:', data.length);
        
        if (data.length < 7) { // Minimum length: 1 byte type + 2 bytes username length + 2 bytes password length + 2 bytes version
            console.log('[PacketParser] Data too short for login request');
            throw new Error('Invalid login request packet');
        }

        let offset = 0;
        
        // Read username length
        const usernameLength = data.readUInt16LE(offset);
        offset += 2;
        console.log('[PacketParser] Username length:', usernameLength);
        
        if (offset + usernameLength > data.length) {
            console.log('[PacketParser] Username length exceeds packet size');
            throw new Error('Invalid login request packet');
        }
        
        // Read username
        const username = data.slice(offset, offset + usernameLength).toString();
        offset += usernameLength;
        console.log('[PacketParser] Username:', username);
        
        // Read password length
        const passwordLength = data.readUInt16LE(offset);
        offset += 2;
        console.log('[PacketParser] Password length:', passwordLength);
        
        if (offset + passwordLength > data.length) {
            console.log('[PacketParser] Password length exceeds packet size');
            throw new Error('Invalid login request packet');
        }
        
        // Read password
        const password = data.slice(offset, offset + passwordLength).toString();
        offset += passwordLength;
        console.log('[PacketParser] Password length:', password.length);

        // Read version
        const version = data.readUInt16LE(offset);
        offset += 2;
        console.log('[PacketParser] Version:', version);

        // Read login code length
        const loginCodeLength = data.readUInt8(offset);
        offset += 1;
        console.log('[PacketParser] Login code length:', loginCodeLength);

        // Read key
        const key = data.readUInt8(offset);
        offset += 1;
        console.log('[PacketParser] Key:', key);

        // Read and decode login code
        if (offset + loginCodeLength > data.length) {
            console.log('[PacketParser] Login code length exceeds packet size');
            throw new Error('Invalid login request packet');
        }

        const loginCodeBuffer = data.slice(offset, offset + loginCodeLength);
        const loginCode = Buffer.from(loginCodeBuffer.map(byte => byte ^ key)).toString();
        console.log('[PacketParser] Login code:', loginCode);
        
        return { username, password, version, loginCode };
    }

    static createLoginResponse(success: boolean, message: string): Buffer {
        const messageBuffer = Buffer.from(message, 'utf8');
        const response = Buffer.alloc(2 + messageBuffer.length);
        
        response.writeUInt8(PacketType.LOGIN_RESPONSE, 0);
        response.writeUInt8(success ? 1 : 0, 1);
        messageBuffer.copy(response, 2);
        
        return response;
    }

    static createLogoutResponse(success: boolean, message: string): Buffer {
        const messageBuffer = Buffer.from(message, 'utf8');
        const response = Buffer.alloc(2 + messageBuffer.length);
        
        response.writeUInt8(PacketType.LOGOUT_RESPONSE, 0);
        response.writeUInt8(success ? 1 : 0, 1);
        messageBuffer.copy(response, 2);
        
        return response;
    }

    static createErrorResponse(message: string): Buffer {
        const messageBuffer = Buffer.from(message, 'utf8');
        const response = Buffer.alloc(1 + messageBuffer.length);
        
        response.writeUInt8(PacketType.ERROR, 0);
        messageBuffer.copy(response, 1);
        
        return response;
    }

    static parseCharacterCreate(data: Buffer): CharacterCreatePacket {
        return {
            name: data.toString('utf8', 1, 17).trim(),
            class: data.readUInt8(17),
            gender: data.readUInt8(18),
            hairStyle: data.readUInt8(19),
            hairColor: data.readUInt8(20)
        };
    }

    static parseCharacterDelete(data: Buffer): CharacterDeletePacket {
        return {
            characterId: data.readUInt32LE(1),
            password: data.toString('utf8', 5, 21).trim()
        };
    }

    static parseMove(data: Buffer): MovePacket {
        return {
            x: data.readFloatLE(1),
            y: data.readFloatLE(5),
            direction: data.readUInt8(9),
            timestamp: data.readUInt32LE(10)
        };
    }

    static parseChat(data: Buffer): ChatPacket {
        const type = data.readUInt8(1);
        let target: string | undefined;
        let messageStart = 2;

        if (type === 1) { // Whisper
            target = data.toString('utf8', 2, 18).trim();
            messageStart = 18;
        }

        return {
            type,
            target,
            message: data.toString('utf8', messageStart).trim()
        };
    }

    static parseAttack(data: Buffer): AttackPacket {
        return {
            targetId: data.readUInt32LE(1),
            skillId: data.readUInt16LE(5),
            timestamp: data.readUInt32LE(7)
        };
    }

    static parseItem(data: Buffer): ItemPacket {
        return {
            itemId: data.readUInt32LE(1),
            slot: data.readUInt8(5),
            count: data.readUInt16LE(6)
        };
    }

    static parseDropItem(data: Buffer): DropItemPacket {
        return {
            itemId: data.readUInt32LE(1),
            slot: data.readUInt8(5),
            count: data.readUInt16LE(6),
            x: data.readFloatLE(8),
            y: data.readFloatLE(12)
        };
    }

    static parseEquipItem(data: Buffer): EquipItemPacket {
        return {
            itemId: data.readUInt32LE(1),
            slot: data.readUInt8(5),
            equipSlot: data.readUInt8(6)
        };
    }

    static parseInventoryUpdate(data: Buffer): InventoryUpdatePacket {
        const itemCount = data.readUInt8(1);
        const items = [];
        let offset = 2;

        for (let i = 0; i < itemCount; i++) {
            items.push({
                itemId: data.readUInt32LE(offset),
                slot: data.readUInt8(offset + 4),
                count: data.readUInt16LE(offset + 5),
                position: data.readUInt8(offset + 7)
            });
            offset += 8;
        }

        return { items };
    }

    static parseEmote(data: Buffer): EmotePacket {
        const emoteId = data.readUInt8(1);
        const hasTarget = data.readUInt8(2) === 1;
        
        return {
            emoteId,
            targetId: hasTarget ? data.readUInt32LE(3) : undefined
        };
    }

    static parseParty(data: Buffer): PartyPacket {
        const type = data.readUInt8(1);
        const targetId = data.readUInt32LE(2);
        const hasPartyId = data.readUInt8(6) === 1;
        
        return {
            type,
            targetId,
            partyId: hasPartyId ? data.readUInt32LE(7) : undefined
        };
    }

    static parseGuild(data: Buffer): GuildPacket {
        const type = data.readUInt8(1);
        let offset = 2;
        
        const result: GuildPacket = { type };
        
        if (type === 0) { // Create
            const nameLength = data.readUInt8(offset);
            result.guildName = data.toString('utf8', offset + 1, offset + 1 + nameLength);
        } else if (type === 1) { // Invite
            result.targetId = data.readUInt32LE(offset);
            offset += 4;
        }
        
        if (type !== 0) { // All except create
            result.guildId = data.readUInt32LE(offset);
        }
        
        return result;
    }
} 