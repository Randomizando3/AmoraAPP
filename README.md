# 📱 AmoraApp — Aplicativo Social & Match em .NET MAUI

O **AmoraApp** é um aplicativo mobile desenvolvido em **.NET MAUI**, inspirado em apps modernos como Tinder e Instagram.  
Ele combina **sistema de match**, **feed social**, **stories**, **amigos**, **galeria de fotos**, **perfil avançado** e integração completa com **Firebase**.

O projeto usa arquitetura **MVVM (CommunityToolkit.MVVM)**, serviços bem modularizados e Firebase (Auth + Realtime Database + Storage).

---

## 🚀 Funcionalidades

### 🔐 Autenticação
- Login e registro usando Firebase Auth  
- Criação automática de perfil  
- Persistência de sessão do usuário  

---

### 🧑‍💼 Perfil Completo
- Nome, bio, cidade, idade, gênero  
- Foto de perfil  
- Galeria com até 6 fotos  
- Upload para Firebase Storage  
- Edição em tela dedicada

---

### ❤️ Discover (Match System)
- Swipe Right → Like  
- Swipe Left → Dislike  
- Match instantâneo quando ambos se curtirem  
- Solicitação de amizade pelo botão “+”  
- Animações suaves no cartão  
- Navegação para galeria completa do usuário

---

### 🫂 Sistema de Amigos
- Enviar solicitação  
- Aceitar / Recusar  
- Ver lista de amigos  
- Amizades refletem no feed e stories

---

### 📰 Feed Social
- Posts com texto e imagem  
- Posts só de amigos + você  
- Likes (toggle)  
- Comentários  
- Contador de solicitações pendentes  
- Stories no topo (como Instagram)

---

### 📸 Stories
- Stories com validade de 24h  
- Stories do usuário e dos amigos  
- Likes por usuário  
- Preview automático no feed

---

### 💬 Chat (estrutura pronta)
- Página base incluída  
- ViewModel preparada para expansão futura  

---

## 🏗️ Arquitetura Geral

O projeto segue o padrão MVVM:

AmoraApp/
│
├── App.xaml / App.xaml.cs
├── AppShell.xaml / AppShell.xaml.cs
│
├── Models/ → Classes de dados (UserProfile, Post, Story, etc.)
├── Views/ → Páginas .xaml (Discover, Feed, Profile, Chat...)
├── ViewModels/ → Lógica MVVM (Commands, States, Bindings)
├── Services/ → FirebaseAuth, FirebaseDatabase, Storage, Match, Friends, Stories
└── Config/ → FirebaseSettings


---

## 🔥 Integração com Firebase

### Firebase usado no projeto:
- **Auth** → Registro/Login  
- **Realtime Database** → Users, Posts, Matches, Friends, Stories  
- **Storage** → Fotos de perfil, galeria e imagens de post  

Configuração armazenada em:

AmoraApp/Config/FirebaseSettings.cs

---

## 🛠️ Tecnologias Utilizadas

- **.NET MAUI (.NET 9)**
- **C# 12**
- **CommunityToolkit.MVVM**
- **Firebase Auth**
- **Firebase Realtime Database**
- **Firebase Storage**
- **XAML**
- **MVVM Pattern**

---

## 📸 Capturas 

---

## 🧩 Como Rodar

### 1. Clone o repositório

```bash
git clone https://github.com/seuusuario/amoraapp.git
cd amoraapp
2. Configure o Firebase
Edite o arquivo:
Config/FirebaseSettings.cs
e preencha com seu:

API Key

Auth Domain

Database URL

Storage Bucket

3. Restaure dependências
bash
dotnet restore


4. Execute no Android/Windows
bash
dotnet build
dotnet maui run -t android
ou para Windows:

bash
dotnet maui run -t windows


🎯 Roadmap Futuro
Sistema de mensagens completo

Filtros avançados no Discover (idade, distância, interesses)

Boosts e Super Likes reais

Stories em vídeo

Notificações push

🤝 Contribuição
Pull requests são bem-vindos!
Sinta-se livre para abrir issues para sugestões ou bugs.

📜 Licença
Este projeto está sob a licença MIT.
Você pode usá-lo, modificá-lo e distribuí-lo livremente.

💖 Agradecimentos
Obrigado por conferir o projeto!
O AmoraApp foi desenvolvido com foco em simplicidade, modernidade e fácil expansão.

---
