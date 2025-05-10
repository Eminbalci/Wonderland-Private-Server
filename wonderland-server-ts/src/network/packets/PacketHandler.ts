import { Socket } from 'net';
import { PacketType } from './PacketTypes';
import { PacketParser } from './PacketParser';
import { PlayerRepository } from '../../database/repositories/PlayerRepository';
import { PlayerSession } from '../PlayerSession';
import { Player } from '../../database/entities/Player';

export class PacketHandler {
    private playerRepository: PlayerRepository;

    constructor() {
        this.playerRepository = new PlayerRepository();
    }

    async handlePacket(socket: Socket, packetType: number, data: Buffer): Promise<void> {
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        if (!session) return;

        switch (packetType) {
            case PacketType.LOGIN_REQUEST:
                await this.handleLogin(socket, data);
                break;
            case PacketType.CHARACTER_CREATE:
                await this.handleCharacterCreate(socket, data);
                break;
            case PacketType.CHARACTER_DELETE:
                await this.handleCharacterDelete(socket, data);
                break;
            case PacketType.CHARACTER_LIST:
                await this.handleCharacterList(socket);
                break;
            case PacketType.MOVE:
                await this.handleMove(socket, data);
                break;
            case PacketType.ATTACK:
                await this.handleAttack(socket, data);
                break;
            case PacketType.CHAT:
                await this.handleChat(socket, data);
                break;
            case PacketType.PING:
                this.handlePing(socket);
                break;
            case PacketType.USE_ITEM:
                await this.handleUseItem(socket, data);
                break;
            case PacketType.PICKUP_ITEM:
                await this.handlePickupItem(socket, data);
                break;
            case PacketType.DROP_ITEM:
                await this.handleDropItem(socket, data);
                break;
            case PacketType.EQUIP_ITEM:
                await this.handleEquipItem(socket, data);
                break;
            case PacketType.UNEQUIP_ITEM:
                await this.handleUnequipItem(socket, data);
                break;
            case PacketType.EMOTE:
                await this.handleEmote(socket, data);
                break;
            case PacketType.PARTY_INVITE:
            case PacketType.PARTY_ACCEPT:
            case PacketType.PARTY_DECLINE:
            case PacketType.PARTY_LEAVE:
                await this.handleParty(socket, data);
                break;
            case PacketType.GUILD_CREATE:
            case PacketType.GUILD_INVITE:
            case PacketType.GUILD_ACCEPT:
            case PacketType.GUILD_DECLINE:
            case PacketType.GUILD_LEAVE:
                await this.handleGuild(socket, data);
                break;
            default:
                console.log(`Unhandled packet type: 0x${packetType.toString(16)}`);
        }
    }

    private async handleLogin(socket: Socket, data: Buffer): Promise<void> {
        const { username, password } = PacketParser.parseLoginRequest(data);

        try {
            const player = await this.playerRepository.findByUsername(username);
            if (player && player.password === password) { // TODO: Implement proper password hashing
                player.isOnline = true;
                await this.playerRepository.update(player);
                
                const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
                if (session) {
                    session.setPlayer(player);
                }
                
                this.sendPacket(socket, PacketType.LOGIN_RESPONSE, Buffer.from([0x01])); // Success
            } else {
                this.sendPacket(socket, PacketType.LOGIN_RESPONSE, Buffer.from([0x00])); // Failed
            }
        } catch (error) {
            console.error('Login error:', error);
            this.sendPacket(socket, PacketType.LOGIN_RESPONSE, Buffer.from([0x00])); // Failed
        }
    }

    private async handleCharacterCreate(socket: Socket, data: Buffer): Promise<void> {
        const { name, class: characterClass, gender, hairStyle, hairColor } = PacketParser.parseCharacterCreate(data);
        
        try {
            // TODO: Validate character creation data
            const player = await this.playerRepository.create(name, "default_password"); // TODO: Handle password properly
            this.sendPacket(socket, PacketType.CHARACTER_CREATE, Buffer.from([0x01])); // Success
        } catch (error) {
            console.error('Character creation error:', error);
            this.sendPacket(socket, PacketType.CHARACTER_CREATE, Buffer.from([0x00])); // Failed
        }
    }

    private async handleCharacterDelete(socket: Socket, data: Buffer): Promise<void> {
        const { characterId, password } = PacketParser.parseCharacterDelete(data);
        
        try {
            const player = await this.playerRepository.findById(characterId);
            if (player && player.password === password) {
                await this.playerRepository.delete(characterId);
                this.sendPacket(socket, PacketType.CHARACTER_DELETE, Buffer.from([0x01])); // Success
            } else {
                this.sendPacket(socket, PacketType.CHARACTER_DELETE, Buffer.from([0x00])); // Failed
            }
        } catch (error) {
            console.error('Character deletion error:', error);
            this.sendPacket(socket, PacketType.CHARACTER_DELETE, Buffer.from([0x00])); // Failed
        }
    }

    private async handleCharacterList(socket: Socket): Promise<void> {
        try {
            const players = await this.playerRepository.findAll();
            // TODO: Format character list data
            const response = Buffer.alloc(1 + players.length * 32); // Adjust size based on your needs
            response.writeUInt8(players.length, 0);
            // TODO: Write character data to response buffer
            this.sendPacket(socket, PacketType.CHARACTER_LIST, response);
        } catch (error) {
            console.error('Character list error:', error);
            this.sendPacket(socket, PacketType.CHARACTER_LIST, Buffer.from([0x00]));
        }
    }

    private async handleMove(socket: Socket, data: Buffer): Promise<void> {
        const { x, y, direction, timestamp } = PacketParser.parseMove(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            const player = session.getPlayer()!;
            player.x = x;
            player.y = y;
            await this.playerRepository.update(player);
            
            // TODO: Broadcast movement to nearby players
            console.log(`Player ${player.username} moved to: ${x}, ${y}`);
        }
    }

    private async handleAttack(socket: Socket, data: Buffer): Promise<void> {
        const { targetId, skillId, timestamp } = PacketParser.parseAttack(data);
        // TODO: Implement attack logic
        console.log(`Attack: target=${targetId}, skill=${skillId}, time=${timestamp}`);
    }

    private async handleChat(socket: Socket, data: Buffer): Promise<void> {
        const { type, target, message } = PacketParser.parseChat(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            const player = session.getPlayer()!;
            console.log(`Chat [${type}] from ${player.username}: ${message}`);
            
            // TODO: Implement chat broadcasting based on type
            // For now, just echo back
            this.sendPacket(socket, PacketType.CHAT, data);
        }
    }

    private handlePing(socket: Socket): void {
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        if (session) {
            session.updateLastPing();
        }
        this.sendPacket(socket, PacketType.PING, Buffer.from([]));
    }

    private async handleUseItem(socket: Socket, data: Buffer): Promise<void> {
        const { itemId, slot, count } = PacketParser.parseItem(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            // TODO: Implement item usage logic
            console.log(`Player using item: ${itemId} from slot ${slot}`);
        }
    }

    private async handlePickupItem(socket: Socket, data: Buffer): Promise<void> {
        const { itemId, slot, count } = PacketParser.parseItem(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            // TODO: Implement item pickup logic
            console.log(`Player picking up item: ${itemId}`);
        }
    }

    private async handleDropItem(socket: Socket, data: Buffer): Promise<void> {
        const { itemId, slot, count, x, y } = PacketParser.parseDropItem(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            // TODO: Implement item drop logic
            console.log(`Player dropping item: ${itemId} at ${x}, ${y}`);
        }
    }

    private async handleEquipItem(socket: Socket, data: Buffer): Promise<void> {
        const { itemId, slot, equipSlot } = PacketParser.parseEquipItem(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            // TODO: Implement item equip logic
            console.log(`Player equipping item: ${itemId} to slot ${equipSlot}`);
        }
    }

    private async handleUnequipItem(socket: Socket, data: Buffer): Promise<void> {
        const { itemId, slot } = PacketParser.parseItem(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            // TODO: Implement item unequip logic
            console.log(`Player unequipping item: ${itemId}`);
        }
    }

    private async handleEmote(socket: Socket, data: Buffer): Promise<void> {
        const { emoteId, targetId } = PacketParser.parseEmote(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            const player = session.getPlayer()!;
            // TODO: Implement emote broadcasting
            console.log(`Player ${player.username} using emote: ${emoteId}`);
        }
    }

    private async handleParty(socket: Socket, data: Buffer): Promise<void> {
        const { type, targetId, partyId } = PacketParser.parseParty(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            const player = session.getPlayer()!;
            // TODO: Implement party system
            console.log(`Party action ${type} from ${player.username} to ${targetId}`);
        }
    }

    private async handleGuild(socket: Socket, data: Buffer): Promise<void> {
        const { type, targetId, guildId, guildName } = PacketParser.parseGuild(data);
        const session = PlayerSession.getSession(`${socket.remoteAddress}:${socket.remotePort}`);
        
        if (session && session.getPlayer()) {
            const player = session.getPlayer()!;
            // TODO: Implement guild system
            console.log(`Guild action ${type} from ${player.username}`);
        }
    }

    private sendPacket(socket: Socket, type: PacketType, data: Buffer): void {
        const packet = Buffer.alloc(data.length + 1);
        packet.writeUInt8(type, 0);
        data.copy(packet, 1);
        socket.write(packet);
    }
} 