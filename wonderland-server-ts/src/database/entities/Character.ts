import { Entity, PrimaryGeneratedColumn, Column, ManyToOne, CreateDateColumn, UpdateDateColumn } from 'typeorm';
import { Player } from './Player';

export enum CharacterClass {
    WARRIOR = 0,
    MAGE = 1,
    ARCHER = 2,
    PRIEST = 3
}

@Entity()
export class Character {
    @PrimaryGeneratedColumn()
    id!: number;

    @Column()
    name!: string;

    @Column()
    level: number = 1;

    @Column()
    experience: number = 0;

    @Column()
    health: number = 100;

    @Column()
    mana: number = 100;

    @Column()
    strength: number = 10;

    @Column()
    dexterity: number = 10;

    @Column()
    intelligence: number = 10;

    @Column()
    vitality: number = 10;

    @Column({ type: 'float' })
    x: number = 0;

    @Column({ type: 'float' })
    y: number = 0;

    @Column({ type: 'float' })
    z: number = 0;

    @Column({ type: 'enum', enum: CharacterClass, default: CharacterClass.WARRIOR })
    class: CharacterClass = CharacterClass.WARRIOR;

    @Column()
    gender: number = 0;

    @Column()
    hairStyle: number = 0;

    @Column()
    hairColor: number = 0;

    @ManyToOne(() => Player, (player: Player) => player.characters)
    player!: Player;

    @CreateDateColumn()
    createdAt!: Date;

    @UpdateDateColumn()
    updatedAt!: Date;

    constructor(name: string, player: Player, characterClass: CharacterClass = CharacterClass.WARRIOR) {
        this.name = name;
        this.player = player;
        this.class = characterClass;
    }
} 