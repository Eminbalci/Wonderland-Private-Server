import { DataSource } from "typeorm";
import { config } from "../config/config";

export const AppDataSource = new DataSource({
    type: "mysql",
    host: config.database.host,
    port: config.database.port,
    username: config.database.username,
    password: config.database.password,
    database: config.database.database,
    synchronize: true, // Set to false in production
    logging: true,
    entities: ["src/database/entities/**/*.ts"],
    migrations: ["src/database/migrations/**/*.ts"],
    subscribers: ["src/database/subscribers/**/*.ts"],
}); 