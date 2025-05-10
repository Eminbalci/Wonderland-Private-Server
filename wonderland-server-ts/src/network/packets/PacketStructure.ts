export interface LoginRequestPacket {
    username: string;
    password: string;
}

export interface CharacterCreatePacket {
    name: string;
    class: number;
    gender: number;
    hairStyle: number;
    hairColor: number;
}

export interface CharacterDeletePacket {
    characterId: number;
    password: string;
}

export interface MovePacket {
    x: number;
    y: number;
    direction: number;
    timestamp: number;
}

export interface ChatPacket {
    type: number; // 0: normal, 1: whisper, 2: party, 3: guild, 4: world
    target?: string; // For whisper messages
    message: string;
}

export interface AttackPacket {
    targetId: number;
    skillId: number;
    timestamp: number;
}

export interface ItemPacket {
    itemId: number;
    slot: number;
    count: number;
}

export interface DropItemPacket {
    itemId: number;
    slot: number;
    count: number;
    x: number;
    y: number;
}

export interface EquipItemPacket {
    itemId: number;
    slot: number;
    equipSlot: number;
}

export interface InventoryUpdatePacket {
    items: Array<{
        itemId: number;
        slot: number;
        count: number;
        position: number;
    }>;
}

export interface EmotePacket {
    emoteId: number;
    targetId?: number;
}

export interface PartyPacket {
    type: number; // 0: invite, 1: accept, 2: decline, 3: leave
    targetId: number;
    partyId?: number;
}

export interface GuildPacket {
    type: number; // 0: create, 1: invite, 2: accept, 3: decline, 4: leave
    targetId?: number;
    guildId?: number;
    guildName?: string;
} 