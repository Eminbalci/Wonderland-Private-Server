import { Entity, PrimaryGeneratedColumn, Column, CreateDateColumn, UpdateDateColumn, OneToMany } from "typeorm";
import { Character } from './Character';

@Entity()
export class Player {
    @PrimaryGeneratedColumn()
    id!: number;

    @Column({ unique: true })
    username!: string;

    @Column()
    password!: string; // Will be hashed before storage

    @Column({ default: false })
    isOnline: boolean = false;

    @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP' })
    lastLogin!: Date;

    @OneToMany(() => Character, (character: Character) => character.player)
    characters!: Character[];

    @CreateDateColumn()
    createdAt!: Date;

    @UpdateDateColumn()
    updatedAt!: Date;

    constructor(username: string, password: string) {
        this.username = username;
        this.password = password;
        this.lastLogin = new Date();
    }

    async getCharacters(): Promise<Character[]> {
        return this.characters;
    }
} 