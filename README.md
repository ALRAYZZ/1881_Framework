# 1881_Framework for FiveM

**Version:** 1.0.0  
**Author:** ALRAYZZ  

---

## Overview

1881_Framework is a lightweight, modular C# framework for FiveM, designed to handle core server functionality in a clean and maintainable way.  
It currently provides separate modules for:

- **PlayerCore** – Handles player registration, loading basic data (name, identifier, last login), and initial setup.  
- **PedManager** – Manages player ped models, including loading from the database or applying defaults.  
- **Database** – A shared resource for low-level database operations.  
- **Armory** – Handles weapons, inventory, and related logic.  

Each module is structured as its own solution inside the framework but is integrated under a single Git repository for easy development and deployment.  

---

## Features

- Modular architecture: each system manages its own domain and persistence.  
- Server-client communication via events, keeping core logic centralized.  
- Default setups for new players (e.g., default ped models, default job).  
- Easy to expand: add new modules or extend existing ones without affecting the core.  

---

## Folder Structure

