export enum PacketType {
    // Login packets
    LOGIN_REQUEST = 0x01,
    LOGIN_RESPONSE = 0x02,
    LOGOUT_REQUEST = 0x03,
    LOGOUT_RESPONSE = 0x04,
    
    // Character packets
    CHARACTER_LIST_REQUEST = 0x10,
    CHARACTER_LIST_RESPONSE = 0x11,
    CHARACTER_CREATE_REQUEST = 0x12,
    CHARACTER_CREATE_RESPONSE = 0x13,
    CHARACTER_DELETE_REQUEST = 0x14,
    CHARACTER_DELETE_RESPONSE = 0x15,
    
    // Game packets
    MOVE_REQUEST = 0x20,
    MOVE_RESPONSE = 0x21,
    CHAT_MESSAGE = 0x22,
    CHAT_RESPONSE = 0x23,
    
    // System packets
    PING = 0xF0,
    PONG = 0xF1,
    ERROR = 0xFF
} 