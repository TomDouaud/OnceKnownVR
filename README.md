# 🏛️ Once Known : The Guide's Apprentices

**Once Known** est un jeu d'exploration et de découverte narratif (Walking Simulator éducatif) conçu pour la réalité virtuelle[cite: 5]. 

Plongé au cœur d'un musée interactif, le joueur n'est plus contraint de lire de longs panneaux explicatifs. À la place, il interagit vocalement et de manière organique avec un **Guide Virtuel propulsé par l'Intelligence Artificielle**, qui l'accompagne, s'adapte à ses émotions et répond dynamiquement à ses questions[cite: 5].

---

## ✨ Fonctionnalités Principales

*   **🎙️ Interaction Vocale Naturelle (Push-to-Talk) :** Parlez directement au guide via le microphone du casque. Un système de *chunking* audio avancé permet un traitement en temps réel pour une latence minimale[cite: 5].
*   **🧠 IA Conversationnelle & Générative :** Les dialogues ne sont pas scriptés. Les réponses sont générées dynamiquement par un modèle de langage (LLM Grok) nourri au contexte historique des œuvres[cite: 5].
*   **🎭 Analyse Comportementale :** Le guide écoute non seulement *ce que* vous dites, mais *comment* vous le dites. Le système détecte l'émotion du joueur (Transformers/Superb ML) et classifie le registre de langage (ML-Agents) pour adapter le ton de la réponse[cite: 5].
*   **🚶‍♂️ Navigation Intelligente :** Grâce au système NavMesh, le guide se déplace organiquement dans le musée pour vous suivre et se positionner face aux œuvres ciblées[cite: 5].

---

## 🛠️ Architecture et Technologies

Pour garantir des performances optimales en réalité virtuelle (FPS constants) et éviter le mal de mouvement, le projet repose sur une architecture hybride[cite: 5] :

**Client (Local - Casque VR) :**
*   **Moteur :** Unity 3D (XR Interaction Toolkit)[cite: 5]
*   **Déplacements IA :** Unity NavMesh[cite: 5]
*   **Comportement :** Unity ML-Agents[cite: 5]
*   **Voix (Text-to-Speech) :** Intégration de Unity Sentis / Kokoro TTS[cite: 5]

**Serveur Backend (Cloud/Distant) :**
*   **API / Routage :** Node.js & Python (Flask)[cite: 5]
*   **Speech-to-Text (STT) :** OpenAI Whisper[cite: 5]
*   **Détection d'émotion :** Modèles Transformers / Superb ML[cite: 5]
*   **Génération de texte :** LLM Grok[cite: 5]

---

## 🎮 Contrôles en VR

*   **Déplacement & Rotation :** Joysticks des contrôleurs.
*   **Interaction physique :** Bouton Grip pour saisir des objets.
*   **Parler au guide (Push-to-Talk) :** Maintenez la Gâchette (Trigger) enfoncée pour parler, relâchez pour envoyer la question à l'IA.
*   **Appel du guide :** Mouvement spécifique (Trigger motion) pour faire apparaître ou congédier le guide[cite: 5].

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

Ce projet a été réalisé dans le cadre du cours *8IAR961 - Intelligence artificielle en développement de jeux vidéo*[cite: 5].

*   **Samuel Martin-Grise :** Intégration LLM, infrastructure serveur Backend et injection de contexte[cite: 5].
*   **Léo Cheikh-Boukal :** Déplacements NavMesh, animations et synchronisation de l'entité IA[cite: 5].
*   **Yanis Reynaud :** Machine Learning (ML-Agents), conception du comportement et de l'arbre de décision[cite: 5].
*   **Tom Douaud :** Création et modélisation de l'environnement 3D, intégration des œuvres d'art[cite: 5].
*   **Tom Dunet :** Contrôles VR, mécanique Push-to-Talk et implémentation du découpage audio (Chunking)[cite: 5].
