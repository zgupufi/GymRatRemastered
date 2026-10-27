--liquibase formatted sql

--changeset gymrat:001-initial-schema
CREATE TABLE "Exercises" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "MainMuscle" character varying(50) NOT NULL,
    "SubMuscles" character varying(200),
    "Equipment" character varying(50),
    "Difficulty" character varying(20),
    "Description" character varying(1000),
    "YoutubeLink" character varying(200),
    CONSTRAINT "PK_Exercises" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Exercises_Name" ON "Exercises" ("Name");

--rollback DROP TABLE "Exercises";
