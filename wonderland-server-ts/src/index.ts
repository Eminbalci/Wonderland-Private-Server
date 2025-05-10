import express from 'express';
import { createServer } from 'http';
import { Server } from 'socket.io';
import { config } from './config/config';
import { AppDataSource } from './database/data-source';
import 'reflect-metadata';

const app = express();
const httpServer = createServer(app);
const io = new Server(httpServer, {
    cors: {
        origin: "*",
        methods: ["GET", "POST"]
    }
});

// Basic middleware
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Basic route
app.get('/', (req, res) => {
    res.json({ message: 'Wonderland Private Server API' });
});

// Socket.IO connection handling
io.on('connection', (socket) => {
    console.log('Client connected:', socket.id);

    socket.on('disconnect', () => {
        console.log('Client disconnected:', socket.id);
    });
});

// Initialize database connection and start server
const PORT = config.server.port;

AppDataSource.initialize()
    .then(() => {
        console.log("Database connection established");
        httpServer.listen(PORT, () => {
            console.log(`Server running on port ${PORT}`);
        });
    })
    .catch((error) => {
        console.error("Error during database initialization:", error);
    }); 