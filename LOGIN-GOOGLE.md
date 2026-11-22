# Login do Google Drive nesta prévia

O responsável pelo OuO criou o projeto **ouo-overclock-informatica**, ativou a API do Drive e criou o cliente **OuO Windows**, do tipo **App para computador**. O cadastro do aplicativo foi integrado ao executável para permitir o login no navegador.

O projeto está no modo **Testando**, com a conta aprovada do proprietário incluída na lista de usuários de teste. Outras contas precisam ser adicionadas pelo responsável durante esse período. A configuração de faturamento não foi ativada nesta tarefa.

## Permissão usada

O único escopo do Google é `https://www.googleapis.com/auth/drive.metadata.readonly`, para consultar informações dos arquivos. Esse acesso permite o inventário necessário ao cálculo de tamanhos e à ordenação das pastas.

A versão 1.0.0.106 também consulta `about.get` da Drive API v2 para obter `quotaBytesByService`. Essa resposta pode identificar o consumo de serviços como Drive, Gmail e Fotos com a mesma autorização de leitura. O OuO mostra somente os serviços e valores presentes nessa resposta, além do saldo não classificado. Essa consulta não habilita APIs de leitura de mensagens ou de conteúdo do Fotos. [Referência oficial](https://developers.google.com/workspace/drive/api/reference/rest/v2/about) · [Autorização do método](https://developers.google.com/workspace/drive/api/reference/rest/v2/about/get)

O Google classifica esse escopo como restrito. A distribuição pública requer o processo de verificação correspondente, com os dados e as páginas públicas do aplicativo. Essa revisão ainda não foi realizada. O modo de testes também pode exigir nova autorização periódica. [Escopos oficiais do Drive](https://developers.google.com/workspace/drive/api/guides/api-specific-auth) · [Requisitos para produção](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance)

## Compilar o código-fonte

O código-fonte mantém o suporte a compilações com e sem cadastro integrado. Para uma compilação própria com login direto, o responsável deve fornecer ao script **Compilar.ps1** o parâmetro **-GoogleOAuthFile**, apontando para o JSON de seu cliente do tipo **App para computador**.

O script integra apenas o identificador e a chave do cadastro do cliente de computador. Ele descarta campos extras do JSON, incluindo eventuais dados de sessão. A autorização de cada usuário fica no perfil do Windows e não faz parte do pacote distribuído. O arquivo JSON original do responsável não é incluído no ZIP.

O uso padrão da API está disponível sem cobrança adicional dentro dos limites publicados. O Google informa limites e possíveis cobranças por excedentes; esta prévia não vincula faturamento nem ativa o teste de crédito. [Limites e preços oficiais](https://developers.google.com/workspace/drive/api/guides/limits#pricing)
