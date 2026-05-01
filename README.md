# 🏛️ Once Known : The Guide's Apprentices

**Once Known** est un jeu d'exploration et de découverte narratif (Walking Simulator éducatif) conçu pour la réalité virtuelle. 

Plongé au cœur d'un musée interactif, le joueur n'est plus contraint de lire de longs panneaux explicatifs. À la place, il interagit vocalement et de manière organique avec un **Guide Virtuel propulsé par l'Intelligence Artificielle**, qui l'accompagne, s'adapte à ses émotions et répond dynamiquement à ses questions.

---

## ✨ Fonctionnalités Principales

*   **🎙️ Interaction Vocale Naturelle (Push-to-Talk) :** Parlez directement au guide via le microphone du casque. Un système de *chunking* audio avancé permet un traitement en temps réel pour une latence minimale.
*   **🧠 IA Conversationnelle & Générative :** Les dialogues ne sont pas scriptés. Les réponses sont générées dynamiquement par un modèle de langage (LLM Grok) nourri au contexte historique des œuvres.
*   **🎭 Analyse Comportementale :** Le guide écoute non seulement *ce que* vous dites, mais *comment* vous le dites. Le système détecte l'émotion du joueur (Transformers/Superb ML) et classifie le registre de langage (ML-Agents) pour adapter le ton de la réponse.
*   **🚶‍♂️ Navigation Intelligente :** Grâce au système NavMesh, le guide se déplace organiquement dans le musée pour vous suivre et se positionner face aux œuvres ciblées.

---

## 🛠️ Architecture et Technologies

Pour garantir des performances optimales en réalité virtuelle (FPS constants) et éviter le mal de mouvement, le projet repose sur une architecture hybride :

**Client (Local - Casque VR) :**
*   **Moteur :** Unity 3D (XR Interaction Toolkit)
*   **Déplacements IA :** Unity NavMesh
*   **Comportement :** Unity ML-Agents
*   **Voix (Text-to-Speech) :** Intégration de Unity Sentis / Kokoro TTS

**Serveur Backend (Cloud/Distant) :**
*   **API / Routage :** Node.js & Python (Flask)
*   **Speech-to-Text (STT) :** OpenAI Whisper
*   **Détection d'émotion :** Modèles Transformers / Superb ML
*   **Génération de texte :** LLM Grok

---

## 🎮 Contrôles en VR

*   **Déplacement & Rotation :** Joysticks des contrôleurs.
*   **Interaction physique :** Bouton Grip pour saisir des objets.
*   **Parler au guide (Push-to-Talk) :** Maintenez la Gâchette (Trigger) enfoncée pour parler, relâchez pour envoyer la question à l'IA.
*   **Appel du guide :** Mouvement spécifique (Trigger motion) pour faire apparaître ou congédier le guide.

---

## 🚀 Installation & Exécution

### Prérequis
*   Un casque Meta Quest.
*   Une connexion Internet active (indispensable pour les requêtes au serveur IA distant).

### Instructions
1.  Téléchargez le fichier `.apk` depuis la section [Releases](lien_vers_les_releases_ici).
2.  Installez l'application sur votre Meta Quest en utilisant [SideQuest](https://sidequestvr.com/) ou Meta Quest Developer Hub (MQDH).
3.  Lancez le jeu depuis l'onglet **Sources Inconnues** de votre bibliothèque d'applications.

*(Note : Assurez-vous que le serveur backend hébergeant l'API d'intelligence artificielle est actuellement en ligne pour profiter de l'expérience conversationnelle).*

---

## 👥 L'Équipe (Groupe 6)

Ce projet a été réalisé dans le cadre du cours *8IAR961 - Intelligence artificielle en développement de jeux vidéo*.

*   **Samuel Martin-Grise :** Intégration LLM, infrastructure serveur Backend et injection de contexte.
*   **Léo Cheikh-Boukal :** Déplacements NavMesh, animations et synchronisation de l'entité IA.
*   **Yanis Reynaud :** Machine Learning (ML-Agents), conception du comportement et de l'arbre de décision.
*   **Tom Douaud :** Création et modélisation de l'environnement 3D, intégration des œuvres d'art.
*   **Tom Dunet :** Contrôles VR, mécanique Push-to-Talk et implémentation du découpage audio (Chunking).
