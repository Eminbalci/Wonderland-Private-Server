import { Repository } from "typeorm";
import { Player } from "../entities/Player";
import { AppDataSource } from "../data-source";

export class PlayerRepository {
    private repository: Repository<Player>;

    constructor() {
        this.repository = AppDataSource.getRepository(Player);
    }

    async create(username: string, password: string): Promise<Player> {
        const player = new Player(username, password);
        return await this.repository.save(player);
    }

    async findById(id: number): Promise<Player | null> {
        return await this.repository.findOneBy({ id });
    }

    async findByUsername(username: string): Promise<Player | null> {
        return await this.repository.findOneBy({ username });
    }

    async update(player: Player): Promise<Player> {
        return await this.repository.save(player);
    }

    async delete(id: number): Promise<void> {
        await this.repository.delete(id);
    }

    async findAll(): Promise<Player[]> {
        return await this.repository.find();
    }

    async findOnlinePlayers(): Promise<Player[]> {
        return await this.repository.findBy({ isOnline: true });
    }
} 