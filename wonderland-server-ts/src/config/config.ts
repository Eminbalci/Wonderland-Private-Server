import dotenv from 'dotenv';

dotenv.config();

export const config = {
    server: {
        port: process.env.PORT || 3000,
        host: process.env.HOST || 'localhost'
    },
    database: {
        host: process.env.DB_HOST || 'localhost',
        port: parseInt(process.env.DB_PORT || '3306'),
        username: process.env.DB_USER || 'root',
        password: process.env.DB_PASSWORD || '',
        database: process.env.DB_NAME || 'wonderland'
    },
    game: {
        maxPlayers: parseInt(process.env.MAX_PLAYERS || '1000'),
        tickRate: parseInt(process.env.TICK_RATE || '20')
    }
}; 